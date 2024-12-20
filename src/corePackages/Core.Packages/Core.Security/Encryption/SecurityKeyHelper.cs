using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Tokens;

namespace Core.Security.Encryption;

    public static class SecurityKeyHelper
    {
        public static SecurityKey CreateSecurityKey(string securityKey) => 
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(securityKey));
    }

