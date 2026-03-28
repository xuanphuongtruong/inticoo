using InticooInspection.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace InticooInspection.Infrastructure.Data
{
    public static class DbSeeder
    {
        public static async Task SeedAsync(UserManager<AppUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Tạo roles
            string[] roles = { "Admin", "Inspector", "User" };
            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                    await roleManager.CreateAsync(new IdentityRole(role));
            }

            // Tạo Admin mặc định
            if (await userManager.FindByEmailAsync("admin@inticoo.com") == null)
            {
                var admin = new AppUser
                {
                    UserName = "admin",
                    Email = "admin@inticoo.com",
                    FullName = "Administrator",
                    IsActive = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(admin, "Admin@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(admin, "Admin");
            }

            // Tạo User mặc định
            if (await userManager.FindByEmailAsync("user@inticoo.com") == null)
            {
                var user = new AppUser
                {
                    UserName = "user",
                    Email = "user@inticoo.com",
                    FullName = "Default User",
                    IsActive = true,
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(user, "User@123");
                if (result.Succeeded)
                    await userManager.AddToRoleAsync(user, "User");
            }
        }
    }
}
