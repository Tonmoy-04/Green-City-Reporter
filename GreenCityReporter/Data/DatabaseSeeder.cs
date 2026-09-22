using GreenCityReporter.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GreenCityReporter.Data
{
    public static class DatabaseSeeder
    {
        public static async Task SeedAsync(IServiceProvider serviceProvider)
        {
            var roleManager = serviceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            var userManager = serviceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var context = serviceProvider.GetRequiredService<ApplicationDbContext>();
            var configuration = serviceProvider.GetRequiredService<IConfiguration>();

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
            // Seed Multiple Admin Users
            // =========================

            var adminSection = configuration.GetSection("SeedUsers:Admins");

            foreach (var adminConfig in adminSection.GetChildren())
            {
                await SeedUserAsync(
                    userManager,
                    adminConfig,
                    "Admin",
                    "System Admin"
                );
            }

            // =========================
            // Seed Sample Citizen
            // =========================

            var citizenSection = configuration.GetSection("SeedUsers:Citizen");

            await SeedUserAsync(
                userManager,
                citizenSection,
                "Citizen",
                "Sample Citizen"
            );

            // =========================
            // Seed Departments
            // =========================

            var departments = new[]
            {
                new Department
                {
                    Name = "WASA",
                    Description = "Water supply and leakage response."
                },

                new Department
                {
                    Name = "City Corporation",
                    Description = "Municipal roads, drainage, waste, and public infrastructure."
                },

                new Department
                {
                    Name = "Fire Service",
                    Description = "Fire and immediate emergency response."
                },

                new Department
                {
                    Name = "Electricity Department",
                    Description = "Electrical infrastructure and street lighting."
                },

                new Department
                {
                    Name = "Gas Authority",
                    Description = "Gas network and leak response."
                },

                new Department
                {
                    Name = "Traffic / Road Authority",
                    Description = "Traffic and road safety response."
                },

                new Department
                {
                    Name = "Other",
                    Description = "General municipal routing."
                }
            };

            foreach (var department in departments)
            {
                var existing = await context.Departments
                    .FirstOrDefaultAsync(d => d.Name == department.Name);

                if (existing == null)
                {
                    context.Departments.Add(department);
                }
            }

            await context.SaveChangesAsync();

            // =========================
            // Seed Categories
            // =========================

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
                if (!await context.Categories.AnyAsync(c => c.Name == category.Name))
                {
                    context.Categories.Add(category);
                }
            }

            await context.SaveChangesAsync();

            // =========================
            // Map Categories to Departments
            // =========================

            var departmentByName = await context.Departments
                .ToDictionaryAsync(d => d.Name);

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
                var category = await context.Categories
                    .FirstOrDefaultAsync(c => c.Name == mapping.Key);

                if (category != null &&
                    departmentByName.TryGetValue(mapping.Value, out var department))
                {
                    category.DefaultDepartmentId = department.Id;
                }
            }

            await context.SaveChangesAsync();
        }

        // =========================
        // Seed User Helper
        // =========================

        private static async Task SeedUserAsync(
            UserManager<ApplicationUser> userManager,
            IConfigurationSection userConfig,
            string role,
            string defaultFullName)
        {
            var email = userConfig["Email"];
            var password = userConfig["Password"];
            var fullName = userConfig["FullName"] ?? defaultFullName;

            if (string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password))
            {
                return;
            }

            var user = await userManager.FindByEmailAsync(email);

            if (user == null)
            {
                user = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    FullName = fullName,
                    EmailConfirmed = true
                };

                var result = await userManager.CreateAsync(user, password);

                if (!result.Succeeded)
                {
                    return;
                }
            }

            if (!await userManager.IsInRoleAsync(user, role))
            {
                await userManager.AddToRoleAsync(user, role);
            }
        }
    }
}