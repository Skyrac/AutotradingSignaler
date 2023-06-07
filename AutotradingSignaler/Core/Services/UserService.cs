using AutotradingSignaler.Core.Services.Interfaces;
using System.Security.Claims;
namespace AutotradingSignaler.Core.Services;

public class UserService : IUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public UserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public long UserId
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.User != null && context?.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                var identifier = context.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.NameIdentifier);
                if (identifier != null)
                {
                    return long.Parse(identifier.Value);
                }
            }

            return -1;
        }
    }

    public string Username
    {
        get
        {
            var context = _httpContextAccessor.HttpContext;
            if (context?.User != null && context?.User.Identity != null && context.User.Identity.IsAuthenticated)
            {
                var identifier = context.User.Claims.FirstOrDefault(x => x.Type == ClaimTypes.Name);
                if (identifier != null)
                {
                    return identifier.Value;
                }
            }

            return string.Empty;
        }
    }
}

