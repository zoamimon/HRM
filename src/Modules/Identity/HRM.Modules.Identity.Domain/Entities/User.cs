namespace HRM.Modules.Identity.Domain.Entities
{
    public class User
    {
        public Guid UserId { get; private set; }
        public string Email { get; private set; }
        public string HashedPassword { get; private set; }
        public DateTime CreatedAt { get; private set; }

        private readonly List<Role> _roles = new();
        public IReadOnlyCollection<Role> Roles => _roles.AsReadOnly();

        private readonly List<UserRefreshToken> _refreshTokens = new();
        public IReadOnlyCollection<UserRefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

        // Private constructor for EF Core
        private User() { }

        public User(Guid id, string email, string hashedPassword)
        {
            UserId = id;
            Email = email ?? throw new ArgumentNullException(nameof(email));
            HashedPassword = hashedPassword ?? throw new ArgumentNullException(nameof(hashedPassword));
            CreatedAt = DateTime.UtcNow;
        }

        public void AddRole(Role role)
        {
            if (role is not null && !_roles.Any(r => r.RoleId == role.RoleId))
            {
                _roles.Add(role);
            }
        }

        public void AddRefreshToken(string token, DateTime expiryTime)
        {
            _refreshTokens.Add(new UserRefreshToken(this.UserId, token, expiryTime));
        }
    }
}
