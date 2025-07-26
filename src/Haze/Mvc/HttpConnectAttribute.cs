using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Microsoft.AspNetCore.Mvc.Routing;

namespace Haze.Mvc;

public class HttpConnectAttribute : HttpMethodAttribute
{
    private static readonly IEnumerable<string> SupportedMethods = ["CONNECT"];

    public HttpConnectAttribute() : base(SupportedMethods) { }


    public HttpConnectAttribute([StringSyntax("Route")] string template) : base(SupportedMethods, template)
    {
        ArgumentNullException.ThrowIfNull(template, nameof(template));
    }
}
