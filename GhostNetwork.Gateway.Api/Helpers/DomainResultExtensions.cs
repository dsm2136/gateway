﻿using System;
using GhostNetwork.Gateway.Facade;
using Microsoft.AspNetCore.Mvc;

namespace GhostNetwork.Gateway.Api.Helpers
{
    public static class DomainResultExtensions
    {
        public static ProblemDetails ToProblemDetails(this DomainResult domainResult)
        {
            if (domainResult.Success)
            {
                throw new InvalidOperationException();
            }

            var details = new ProblemDetails
            {
                Title = "Domain error"
            };

            details.Extensions["errors"] = new
            {
                domain = domainResult.Errors
            };

            return details;
        }
    }
}