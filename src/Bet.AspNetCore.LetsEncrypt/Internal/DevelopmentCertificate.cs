﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Bet.AspNetCore.LetsEncrypt.Internal
{
    public class DevelopmentCertificate
    {
        // see https://github.com/aspnet/Common/blob/61320f4ecc1a7b60e76ca8fe05cd86c98778f92c/shared/Microsoft.AspNetCore.Certificates.Generation.Sources/CertificateManager.cs#L19-L20
        // This is the unique OID for the developer cert generated by VS and the .NET Core CLI
        private readonly string _aspNetHttpsOid = "1.3.6.1.4.1.311.84.1.1";
        private readonly string _aspNetHttpsOidFriendlyName = "ASP.NET Core HTTPS development certificate";

        private readonly IHostEnvironment _environment;
        private readonly ILogger<DevelopmentCertificate> _logger;

        public DevelopmentCertificate(
            IHostEnvironment environment,
            ILogger<DevelopmentCertificate> logger)
        {
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public IEnumerable<X509Certificate2> GetCertificates()
        {
            if (!_environment.IsDevelopment())
            {
                return Enumerable.Empty<X509Certificate2>();
            }

            var certs = FindDeveloperCert();

            return certs;
        }

        private IEnumerable<X509Certificate2> FindDeveloperCert()
        {
            using var store = new X509Store(StoreName.My, StoreLocation.CurrentUser);
            store.Open(OpenFlags.ReadOnly);
            var certs = store.Certificates.Find(X509FindType.FindByExtension, _aspNetHttpsOid, validOnly: false);
            if (certs.Count == 0)
            {
                _logger.LogDebug("Could not find the " + _aspNetHttpsOidFriendlyName);
            }
            else
            {
                _logger.LogDebug("Using the " + _aspNetHttpsOidFriendlyName + " for 'localhost' requests");

                foreach (var cert in certs)
                {
                    yield return cert;
                }
            }
        }
    }
}