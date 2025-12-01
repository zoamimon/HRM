namespace HRM.Modules.Identity.Domain.Entities
{
    public class UserRefreshToken
    {
        public int UserRefreshTokenId { get; private set; }
        public Guid UserId { get; private set; }
        public string Token { get; private set; }
        public DateTime Expires { get; private set; }
        public DateTime Created { get; private set; }

        public bool IsExpired => DateTime.UtcNow >= Expires;
        public bool IsActive => !IsExpired; // Can add more conditions like IsRevoked

        private UserRefreshToken() { }

        public UserRefreshToken(Guid userId, string token, DateTime expires)
        {
            UserId = userId;
            Token = token;
            Expires = expires;
            Created = DateTime.UtcNow;
        }

        public void Update(string newToken, DateTime newExpiryTime)
        {
            Token = newToken;
            Expires = newExpiryTime;
        }
    }
}
