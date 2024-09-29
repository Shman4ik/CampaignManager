using CampaignManager.Web.Authorozation;
using CampaignManager.Web.Model;
using Microsoft.EntityFrameworkCore;
namespace CampaignManager.Web.Services;

public class UserRegistrationService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ILogger<UserRegistrationService> _logger;

    public UserRegistrationService(ApplicationDbContext dbContext, ILogger<UserRegistrationService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<(bool Success, string Message)> RegisterUserAsync(string email)
    {
        if (string.IsNullOrEmpty(email))
        {
            _logger.LogWarning("Attempt to register user with empty email");
            return (false, "Email is required");
        }

        if (!IsValidEmail(email))
        {
            _logger.LogWarning($"Attempt to register user with invalid email: {email}");
            return (false, "Invalid email format");
        }

        try
        {
            using var transaction = await _dbContext.Database.BeginTransactionAsync();

            var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                user = new ApplicationUser { Email = email, UserName = email, Role = "Игрок" };
                _dbContext.Users.Add(user);
                _logger.LogInformation($"Created new user with email: {email}");
            }
            else if (string.IsNullOrEmpty(user.Role))
            {
                user.Role = "Игрок";
                _logger.LogInformation($"Updated role for existing user with email: {email}");
            }
            else
            {
                _logger.LogInformation($"User with email {email} already exists and has a role");
                return (true, "User already registered");
            }

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation($"User with email {email} successfully registered");
            return (true, "Successfully registered as user");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error occurred while registering user with email: {email}");
            return (false, "An error occurred during registration");
        }
    }

    private bool IsValidEmail(string email)
    {
        try
        {
            var addr = new System.Net.Mail.MailAddress(email);
            return addr.Address == email;
        }
        catch
        {
            return false;
        }
    }
}