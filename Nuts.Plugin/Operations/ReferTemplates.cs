using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nuts.Plugin.Operations
{
    internal static class ReferTemplates
    {
        internal static readonly string GET_DID_TEMPLATE =
            @"{{
                ""query"": {{
                    ""@context"":[
                         ""https://www.w3.org/2018/credentials/v1"",
                         ""https://nuts.nl/credentials/v1"" 
                    ],
                    ""type"":[
                         ""VerifiableCredential"",
            ""NutsOrganizationCredential""
                    ],
                    ""credentialSubject"": {{
                        ""organization"": 
                        {{
                            ""name"": ""{0}""
                        }}
                    }}
                }}
            }}";

        internal static readonly string CREATE_VC_TEMPLATE =
            @"{{
    ""issuer"": ""{0}"", 
    ""type"": ""NutsAuthorizationCredential"",
    ""credentialSubject"": {{
        ""id"": ""{1}"", 
        ""resources"": [
            {{
                ""path"": ""/task/{2}"", 
                ""operations"": [""read"", ""update""],
                ""userContext"": false
            }}
        ],        ""purposeOfUse"": ""bgz-sender""     
    }},
    ""visibility"": ""private""
}}";


        internal static readonly string GET_AT_TEMPLATE =
            @"{{
    ""authorizer"": ""{0}"",
    ""requester"": ""{1}"",
    ""service"": ""bgz-receiver""
}}";


        internal static readonly string GET_AT_EX_TEMPLATE =
            @"{{
    ""authorizer"": ""{0}"",
    ""requester"": ""{1}"",
    ""service"": ""bgz-receiver"",
""credentials"":[{2}]
}}";
    }
}
