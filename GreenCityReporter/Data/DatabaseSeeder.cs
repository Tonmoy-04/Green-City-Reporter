using GreenCityReporter.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace GreenCityReporter.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();

            // =========================
            // Seed Roles
            // =========================

            string[] roles = { "Admin", "Citizen" };

            foreach (var role in roles)
            {
                if (!await roleManager.RoleExistsAsync(role))
                {
                    await roleManager.CreateAsync(new IdentityRole(role));
                }
            }

            // =========================
            // Seed Admin User
            // =========================

            var adminUser = new ApplicationUser
            {
                UserName = "admin@greencity.com",
                Email = "admin@greencity.com",
                FullName = "System Admin",
                EmailConfirmed = true
            };

            if (await userManager.FindByEmailAsync(adminUser.Email) == null)
            {
                var result = await userManager.CreateAsync(
                    adminUser,
                    "Admin@123"
                );

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(
                        adminUser,
                        "Admin"
                    );
                }
            }

            // =========================
            // Seed Citizen User
            // =========================

            var citizenUser = new ApplicationUser
            {
                UserName = "citizen@example.com",
                Email = "citizen@example.com",
                FullName = "Sample Citizen",
                EmailConfirmed = true
            };

            if (await userManager.FindByEmailAsync(citizenUser.Email) == null)
            {
                var result = await userManager.CreateAsync(
                    citizenUser,
                    "Citizen@123"
                );

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(
                        citizenUser,
                        "Citizen"
                    );
                }
            }

            // =========================
            // Seed Categories
            // =========================

            var departments = new[]
            {
                new Department { Name = "WASA", Description = "Water supply and leakage response." },
                new Department { Name = "City Corporation", Description = "Municipal roads, drainage, waste, and public infrastructure." },
                new Department { Name = "Fire Service", Description = "Fire and immediate emergency response." },
                new Department { Name = "Electricity Department", Description = "Electrical infrastructure and street lighting." },
                new Department { Name = "Gas Authority", Description = "Gas network and leak response." },
                new Department { Name = "Traffic / Road Authority", Description = "Traffic and road safety response." },
                new Department { Name = "Other", Description = "General municipal routing." }
            };

            foreach (var department in departments)
            {
                var existing = await context.Departments.FirstOrDefaultAsync(d => d.Name == department.Name);
                if (existing == null)
                {
                    context.Departments.Add(department);
                }
            }

            await context.SaveChangesAsync();

            var categories = new[]
            {
                new Category
                {
                    Name = "Waste Management",
                    Description = "Issues related to waste collection, disposal, and accumulation."
                },

                new Category
                {
                    Name = "Road Damage",
                    Description = "Damaged roads, potholes, and related road problems."
                },

                new Category
                {
                    Name = "Drainage",
                    Description = "Blocked or damaged drainage systems."
                },

                new Category
                {
                    Name = "Street Lighting",
                    Description = "Broken or non-functioning street lights."
                },

                new Category
                {
                    Name = "Waterlogging",
                    Description = "Water accumulation and flooding in public areas."
                },

                new Category
                {
                    Name = "Public Infrastructure",
                    Description = "Issues involving public buildings, facilities, and infrastructure."
                }
            };

            foreach (var category in categories)
            {
                if (!await context.Categories.AnyAsync(
                    c => c.Name == category.Name))
                {
                    context.Categories.Add(category);
                }
            }

            await context.SaveChangesAsync();

            var departmentByName = await context.Departments.ToDictionaryAsync(d => d.Name);
            var categoryMappings = new Dictionary<string, string>
            {
                ["Waste Management"] = "City Corporation",
                ["Road Damage"] = "City Corporation",
                ["Drainage"] = "City Corporation",
                ["Street Lighting"] = "Electricity Department",
                ["Waterlogging"] = "City Corporation",
                ["Public Infrastructure"] = "City Corporation"
            };

            foreach (var mapping in categoryMappings)
            {
                var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == mapping.Key);
                if (category != null && departmentByName.TryGetValue(mapping.Value, out var department))
                {
                    category.DefaultDepartmentId = department.Id;
                }
            }

            await context.SaveChangesAsync();
        }
    }
}