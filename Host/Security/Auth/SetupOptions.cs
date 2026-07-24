namespace Telechron.Host.Security.Auth;

// R-SEC6: the one-time bootstrap credential that gates creating Telechron's
// very first Admin user. There is no public self-registration endpoint --
// SetupController is the only way to create a User before one exists, and
// it requires this token, which the deploying operator supplies out of
// band (env var / config), never a hardcoded default. Treat this exactly
// like a root password: generate it randomly, set it once via
// TELECHRON_SETUP_TOKEN, and either rotate or delete it once the first
// Admin account exists (the endpoint refuses to run a second time anyway,
// see SetupController, but removing the token is still good practice --
// it's meaningless once bootstrap is complete).
public sealed class SetupOptions
{
    public string? SetupToken { get; set; }
}
