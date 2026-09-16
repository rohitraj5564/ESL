using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace ESL_Api.Global
{
    public class RemoveServerHeaderHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);

            // Strip server fingerprinting headers
            response.Headers.Remove("X-AspNet-Version");
            response.Headers.Remove("X-AspNetMvc-Version");
            response.Headers.Remove("Server");
            response.Headers.Remove("X-Powered-By");

            // Attach standard VAPT / OWASP defense headers
            if (!response.Headers.Contains("X-Content-Type-Options"))
            {
                response.Headers.Add("X-Content-Type-Options", "nosniff");
            }
            if (!response.Headers.Contains("X-Frame-Options"))
            {
                response.Headers.Add("X-Frame-Options", "DENY");
            }
            if (!response.Headers.Contains("X-XSS-Protection"))
            {
                response.Headers.Add("X-XSS-Protection", "1; mode=block");
            }
            if (!response.Headers.Contains("Referrer-Policy"))
            {
                response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
            }

            // Ensure CORS headers are present on all responses
            string origin = null;
            if (request.Headers.Contains("Origin"))
            {
                origin = System.Linq.Enumerable.FirstOrDefault(request.Headers.GetValues("Origin"));
            }
            if (string.IsNullOrEmpty(origin))
            {
                origin = "*";
            }

            if (!response.Headers.Contains("Access-Control-Allow-Origin"))
            {
                response.Headers.Add("Access-Control-Allow-Origin", origin);
            }
            if (!response.Headers.Contains("Access-Control-Allow-Headers"))
            {
                response.Headers.Add("Access-Control-Allow-Headers", "Content-Type, Authorization, X-Api-Version, Accept, Origin, X-Requested-With");
            }
            if (!response.Headers.Contains("Access-Control-Allow-Methods"))
            {
                response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, PUT, DELETE, OPTIONS");
            }

            return response;
        }
    }
}