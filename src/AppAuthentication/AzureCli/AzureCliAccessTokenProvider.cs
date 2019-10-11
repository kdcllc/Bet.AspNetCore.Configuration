﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

using AppAuthentication.Helpers;
using AppAuthentication.Models;

using Microsoft.Azure.Services.AppAuthentication;

namespace AppAuthentication.AzureCli
{
    /// <summary>
    /// Gets a token using Azure CLI 2.0 for local development scenarios.
    /// az account get-access-token --resource.
    /// </summary>
    internal class AzureCliAccessTokenProvider : NonInteractiveAzureServiceTokenProviderBase, IAccessTokenProvider
    {
        // Allows for unit testing, by mocking IProcessManager
        private readonly IProcessManager _processManager;

        // Constants related to command to execute to get token using Azure CLI
        private const string Cmd = "cmd.exe";
        private const string Bash = "/bin/bash";
        private const string GetTokenCommand = "az account get-access-token -o json";
        private const string ResourceArgumentName = "--resource";

        // This is the path that a develop can set to tell this class what the install path for Azure CLI is.
        private const string AzureCliPath = "AzureCLIPath";

        // The default install paths are used to find Azure CLI. This is for security, so that any path in the calling program's Path environment is not used to execute Azure CLI.
        private readonly string _azureCliDefaultPathWindows =
            $"{Environment.GetEnvironmentVariable("ProgramFiles(x86)")}\\Microsoft SDKs\\Azure\\CLI2\\wbin; {Environment.GetEnvironmentVariable("ProgramFiles")}\\Microsoft SDKs\\Azure\\CLI2\\wbin"
        ;

        // Default path for non-Windows.
        private const string AzureCliDefaultPath = "/usr/bin:/usr/local/bin";

        internal AzureCliAccessTokenProvider(IProcessManager processManager)
        {
            _processManager = processManager;
            PrincipalUsed = new Principal { Type = "User" };
        }

        public async Task<AuthenticationToken> GetAuthResultAsync(string resource, string authority)
        {
            try
            {
                // Validate resource, since it gets sent as a command line argument to Azure CLI
                ValidationHelper.ValidateResource(resource);

                // Execute Azure CLI to get token
                var response = await _processManager.ExecuteAsync(new Process { StartInfo = GetProcessStartInfo(resource) }).ConfigureAwait(false);

                // Parse the response
                var tokenResponse = TokenResponse.Parse(response);

                var accessToken = tokenResponse.AccessToken2;

                var token = AccessToken.Parse(accessToken);

                PrincipalUsed.IsAuthenticated = true;

                if (token != null)
                {
                    // Set principal used based on the claims in the access token.
                    PrincipalUsed.UserPrincipalName = !string.IsNullOrEmpty(token.Upn) ? token.Upn : token.Email;
                    PrincipalUsed.TenantId = token.TenantId;
                }

                var authResult = Models.AppAuthenticationResult.Create(tokenResponse, TokenResponse.DateFormat.DateTimeString);


                var authenticationToken = new AuthenticationToken
                {
                    AccessToken = authResult.AccessToken,
                    TokenType = authResult.TokenType,
                    Resource = authResult.Resource,
                    ExpiresOn = authResult.ExpiresOn.ToString(),
                    ExpiresIn = token.ExpiryTime.ToString(),
                    ExtExpiresIn = token.ExpiryTime.ToString(),
                    RefreshToken = tokenResponse.AccessToken2
                };

                return authenticationToken;
            }
            catch (Exception exp)
            {
                throw new Exception(
                    $"{nameof(AzureCliAccessTokenProvider)} not able to obtain the token for {ConnectionString} , {resource} , {authority}, {exp.Message}");
            }
        }

        private ProcessStartInfo GetProcessStartInfo(string resource)
        {
            ProcessStartInfo startInfo;

#if FullNetFx
                 startInfo = new ProcessStartInfo
                    {
                        FileName = Path.Combine(Environment.SystemDirectory, Cmd),
                        Arguments = $"/c {GetTokenCommand} {ResourceArgumentName} {resource}"
                    };

            // Default install location for Az CLI is included. If developer has installed to non-default location, the path can be specified using AzureCliPath variable.
            startInfo.EnvironmentVariables["PATH"] = $"{Environment.GetEnvironmentVariable(AzureCliPath)};{_azureCliDefaultPathWindows}";
#else
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = Path.Combine(EnvironmentHelper.SystemDirectory, Cmd),
                    Arguments = $"/c {GetTokenCommand} {ResourceArgumentName} {resource}"
                };

                startInfo.Environment["PATH"] = $"{Environment.GetEnvironmentVariable(AzureCliPath)};{_azureCliDefaultPathWindows}";
            }
            else
            {
                startInfo = new ProcessStartInfo
                {
                    FileName = Bash,
                    Arguments = $"{GetTokenCommand} {ResourceArgumentName} {resource}"
                };

                startInfo.Environment["PATH"] = $"{Environment.GetEnvironmentVariable(AzureCliPath)}:{AzureCliDefaultPath}";
            }
#endif
            return startInfo;
        }
    }
}
