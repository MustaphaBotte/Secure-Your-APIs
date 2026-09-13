using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;
namespace StudentApi.Authorization
{
    public class StudentOwnerOrAdminHandler :AuthorizationHandler<StudentOwnerOrAdminRequirement,int>
    {
        protected override  Task HandleRequirementAsync(AuthorizationHandlerContext context,
                                                  StudentOwnerOrAdminRequirement requirement,int StudentID)
        {

            if (context.User.IsInRole("Admin"))
            {
                context.Succeed(requirement);
                return Task.CompletedTask;
            }

            var identifier = context.User.FindFirst(ClaimTypes.NameIdentifier);

            if(int.TryParse(identifier?.Value,out int authenticatedStudentId) && StudentID == authenticatedStudentId)
            {
                context.Succeed(requirement);
                return Task.CompletedTask;

            }
            return Task.CompletedTask;
        }
    }
}
