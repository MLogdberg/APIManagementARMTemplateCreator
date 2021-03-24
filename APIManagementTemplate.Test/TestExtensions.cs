using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using System.ComponentModel;

namespace APIManagementTemplate.Test
{
    public enum Arm
    {
        [Description("dependsOn")]
        DependsOn,
        [Description("properties")]
        Properties,
        [Description("name")]
        Name,
        [Description("parameters")]
        Parameters,
        [Description("contentValue")]
        ContentValue,
        [Description("policyContent")]
        PolicyContent,
        [Description("type")]
        Type,
        [Description("loggerType")]
        LoggerType,
        [Description("credentials")]
        Credentials,
        [Description("connectionString")]
        ConnectionString,
        [Description("instrumentationKey")]
        InstrumentationKey,
        [Description("variables")]
        Variables,
        [Description("virtualNetworkConfiguration")]
        VirtualNetworkConfiguration,
        [Description("subnetResourceId")]
        SubnetResourceId,
        [Description("vnetid")]
        VnetId,
        [Description("subnetname")]
        SubnetName,
        [Description("virtualNetworkType")]
        VirtualNetworkType,
        [Description("data")]
        Data,
        [Description("password")]
        Password,
        [Description("displayName")]
        DisplayName,
        [Description("builtIn")]
        BuiltIn,
        [Description("apiVersionSetId")]
        ApiVersionSetId,
        [Description("defaultValue")]
        DefaultValue,
        [Description("host")]
        Host,
        [Description("contentFormat")]
        ContentFormat,
        [Description("alwaysLog")]
        AlwaysLog,
        [Description("apiVersion")]
        ApiVersion,
        [Description("loggerId")]
        LoggerId,
        [Description("sampling")]
        Sampling,
        [Description("percentage")]
        Percentage,
        [Description("clientId")]
        ClientId,
        [Description("clientSecret")]
        ClientSecret,
        [Description("metadataEndpoint")]
        MetadataEndpoint,
        [Description("location")]
        Location,
        [Description("kind")]
        Kind,
        [Description("Application_Type")]
        ApplicationType,
        [Description("query")]
        Query,
        [Description("code")]
        Code,
        [Description("url")]
        Url,
        [Description("basePath")]
        BasePath,
        [Description("schemes")]
        Schemes,
        [Description("repoBaseUrl")]
        RepoBaseUrl,
        [Description("_artifactsLocationSasToken")]
        ArtifactsLocationSASToken,
        [Description("mode")]
        Mode,
        [Description("uri")]
        Uri,
        [Description("templateLink")]
        TemplateLink,
        [Description("contentVersion")]
        ContentVersion,
        [Description("copy")]
        Copy,
        [Description("count")]
        Count,
        [Description("resources")]
        Resources
    }

    public enum Filename
    {
        [Description("api-Versioned-HTTP-bin-API.v1.template.json")]
        HttpBinV1,
        [Description("api-Versioned-HTTP-bin-API.v2.template.json")]
        HttpBinV2,
        [Description("api-Versioned-HTTP-bin-API.master.template.json")]
        HttpBinMaster,
        [Description("api-Versioned-HTTP-bin-API.v2.swagger.template.json")]
        HttpBinV2SwaggerTemplate,
        [Description("api-Versioned-HTTP-bin-API.v1.swagger.json")]
        HttpBinV1Swagger,
        [Description("api-Echo-API.template.json")]
        Echo,
        [Description("api-Echo-API.swagger.json")]
        EchoSwagger,
        [Description("api-Versioned-HTTP-bin-API.version-set.template.json")]
        HttpBinVersionSet,
        [Description("service.template.json")]
        Service,
        [Description("groups.template.json")]
        Groups,
        [Description("product-starter.template.json")]
        ProductStarter,
        [Description("product-unlimited.template.json")]
        ProductUnlimited,
        [Description("master.template.json")]
        MasterTemplate,
        [Description("api-Echo-API.create-resource.policy.xml")]
        EchoCreateResourcePolicy,
        [Description("api-TFS.template.json")]
        TFSTemplate,
        [Description("api-TFS.swagger.template.json")]
        TFSSwaggerTemplate,
    }

    public enum ResourceType
    {
        [Description("Microsoft.ApiManagement/service/products/policies")]
        ProductPolicy,
        [Description("Microsoft.ApiManagement/service/products/groups")]
        ProductGroup,
        [Description("Microsoft.ApiManagement/service/groups")]
        Group,
        [Description("Microsoft.ApiManagement/service/policies")]
        Policy,
        [Description("Microsoft.ApiManagement/service/apis")]
        Api,
        [Description("Microsoft.ApiManagement/service")]
        Service,
        [Description("Microsoft.ApiManagement/service/policies")]
        ServicePolicy,
        [Description("Microsoft.ApiManagement/service/loggers")]
        Logger,
        [Description("Microsoft.ApiManagement/service/apis/schemas")]
        Schema,
        [Description("Microsoft.ApiManagement/service/backends")]
        Backend,
        [Description("Microsoft.ApiManagement/service/openidConnectProviders")]
        OpenIdConnectProvider,
        [Description("Microsoft.ApiManagement/service/identityProviders")]
        IdentityProvider,
        [Description("Microsoft.ApiManagement/service/certificates")]
        Certificate,
        [Description("Microsoft.ApiManagement/service/products/policies")]
        Operation,
        [Description("Microsoft.ApiManagement/service/apis/operations/policies")]
        OperationPolicy,
        [Description("Microsoft.ApiManagement/service/apis/policies")]
        ApiPolicy,
        [Description("Microsoft.Insights/components")]
        ApplicationInsights,
        [Description("Microsoft.ApiManagement/service/diagnostics")]
        Diagnostic,
        [Description("Microsoft.ApiManagement/service/apis/diagnostics")]
        ApiDiagnostic,
        [Description("Microsoft.ApiManagement/service/products/apis")]
        ProductApi,
        [Description("Microsoft.ApiManagement/service/products")]
        Product,
        [Description("Microsoft.Resources/deployments")]
        Deployment,
        [Description("Microsoft.ApiManagement/service/namedValues")]
        NamedValues,
    }

    public enum Property
    {
        [Description("name")]
        Name,
        [Description("type")]
        Type,
    }

    public static class TestExtensions
    {
        public static GeneratedTemplate With(this IEnumerable<GeneratedTemplate> templates, Filename filename)
        {
            return templates.FirstOrDefault(x => x.FileName == filename.ToDescription());
        }

        public static JToken WithDirectResource(this GeneratedTemplate template, Enum resourceType, Property property = Property.Type)
        {
            return template.Content.WithDirectResource(resourceType, property);
        }

        public static IEnumerable<JToken> WithDirectResources(this GeneratedTemplate template, Enum resourceType, Property property = Property.Type)
        {
            return template.Content.WithDirectResources(resourceType, property);
        }

        public static JToken WithDirectResource(this JToken jtoken, Enum resourceType, Property property = Property.Type)
        {
            return jtoken.SelectToken($"$.resources[?(@.{property.ToDescription()}=='{resourceType.ToDescription()}')]");
        }

        public static IEnumerable<JToken> WithDirectResources(this JToken jtoken, Enum resourceType, Property property = Property.Type)
        {
            return jtoken.SelectTokens($"$.resources[?(@.{property.ToDescription()}=='{resourceType.ToDescription()}')]");
        }

        public static IEnumerable<JToken> WithResources(this GeneratedTemplate template, ResourceType resourceType, Property property = Property.Type)
        {
            return template.Content.WithResources(resourceType, property);
        }

        public static IEnumerable<JToken> WithResources(this JToken jtoken, ResourceType resourceType, Property property = Property.Type)
        {
            return jtoken.SelectTokens($"$..resources[?(@.{property.ToDescription()}=='{resourceType.ToDescription()}')]");
        }
        public static JToken WithResource(this JToken jtoken, ResourceType resourceType, Property property = Property.Type)
        {
            return jtoken.SelectToken($"$..resources[?(@.{property.ToDescription()}=='{resourceType.ToDescription()}')]");
        }

        public static JToken Index(this JToken token, Arm property)
        {
            return token[property.ToDescription()];
        }

        public static bool Contains(this string @string, Enum value)
        {
            return @string.Contains(value.ToDescription());
        }

        public static string Value(this JToken token, Arm property)
        {
            return token.Value<string>(property.ToDescription());
        }

        public static T ValueWithType<T>(this JToken token, Arm property)
        {
            return token.Value<T>(property.ToDescription());
        }

        public static IEnumerable<string> DependsOn(this JToken token)
        {
            return token.Value<JArray>(Arm.DependsOn.ToDescription()).Values<string>();
        }

        public static string ToDescription(this Enum val)
        {
            DescriptionAttribute[] attributes = (DescriptionAttribute[])val
                .GetType()
                .GetField(val.ToString())
                .GetCustomAttributes(typeof(DescriptionAttribute), false);
            return attributes.Length > 0 ? attributes[0].Description : string.Empty;
        }
    }
}