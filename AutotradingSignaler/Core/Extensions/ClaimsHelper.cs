using System.Security.Claims;

namespace AutotradingSignaler.Core.Extensions
{
    public static class ClaimsHelper
    {
        public static long GetUserId(this ClaimsPrincipal User)
        {
            return User != null && User.Claims != null && long.TryParse(User.Claims.FirstOrDefault(i => i.Type.Equals(ClaimTypes.NameIdentifier))?.Value, out long id) ? id : -1;
        }

        public static string GetUsername(this ClaimsPrincipal User)
        {
            return User != null && User.Claims != null ? User.Claims.FirstOrDefault(i => i.Type.Equals(ClaimTypes.Name))?.Value : null;
        }
    }
}
