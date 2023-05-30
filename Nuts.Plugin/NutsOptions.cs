﻿namespace Nuts.Plugin
{
    public class NutsOptions
    {
        /// <summary>
        /// The name of the organization where this FHIR server is running and by which is known to Nuts.
        /// </summary>
        public string OrganizationName { get; set; }

        /// <summary>
        /// The URL of the Nuts node this FHIR server collaborates with
        /// </summary>
        public string NodeUrl { get; set; }
    }
}
