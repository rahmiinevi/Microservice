﻿using Auth.Application.Enums;
using Auth.Application.Ports.Repositories;
using Auth.Application.Ports.Services;
using Auth.Application.UseCases.Login.Request;
using Auth.Application.UseCases.Login.Response;
using Auth.Domain;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Threading.Tasks;

namespace Auth.Application.UseCases.Login
{
    public class LoginUseCase : ILoginUseCase
    {
        private readonly IOptions<AppSettings> _settings;
        private readonly ILogger _logger;
        private readonly IAuthTokenService _authTokenService;
        private readonly IAuthRepository _authRepository;
        private readonly ICryptographyService _cryptographyService;

        public LoginUseCase(
            IOptions<AppSettings> settings,
            ILogger<LoginUseCase> logger,
            IAuthTokenService authTokenService,
            IAuthRepository authRepository,
            ICryptographyService cryptographyService)
        {
            _settings = settings;
            _logger = logger;
            _authTokenService = authTokenService;
            _authRepository = authRepository;
            _cryptographyService = cryptographyService;
        }

        public async Task<LoginResponse> Execute(LoginRequest request)
        {
            try
            {
                var user = await _authRepository.GetUserByEmail(request.Email);
                if(user == null)
                {
                    var response = new LoginErrorResponse
                    {
                        Message = Enum.GetName(ErrorCodes.UserDoesNotExist),
                        Code = ErrorCodes.UserDoesNotExist.ToString("D")
                    };
                    return response;
                }

                if (AreCredentialsValid(request.Password, user))
                {
                    user.RefreshToken.Value = await _authTokenService.GenerateRefreshToken(_settings.Value.RefreshTokenSettings.Length);
                    user.RefreshToken.Active = true;
                    user.RefreshToken.ExpirationDate = DateTime.UtcNow.AddMinutes(_settings.Value.RefreshTokenSettings.LifeTimeInMinutes);
                    await _authRepository.UpdateUser(user);

                    var token = await _authTokenService.GenerateToken(user);

                    var response = new LoginSuccessResponse
                    {
                        AccessToken = token,
                        RefreshToken = user.RefreshToken.Value
                    };

                    return response;
                }
                else
                {
                    var response = new LoginErrorResponse
                    {
                        Message = Enum.GetName(ErrorCodes.CredentialsAreNotValid),
                        Code = ErrorCodes.CredentialsAreNotValid.ToString("D")
                    };

                    return response;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, ex.Message);

                var response = new LoginErrorResponse
                {
                    Message = Enum.GetName(ErrorCodes.AnUnexpectedErrorOcurred),
                    Code = ErrorCodes.AnUnexpectedErrorOcurred.ToString()
                };

                return response;
            }
        }

        private bool AreCredentialsValid(string testPassword, User user)
        {
            var hash = _cryptographyService.HashPassword(testPassword, user.Salt);
            return hash == user.Password;
        }
    }
}