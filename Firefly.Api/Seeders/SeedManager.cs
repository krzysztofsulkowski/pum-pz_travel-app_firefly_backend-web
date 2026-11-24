namespace Firefly.Api.Seeders
{
    public class SeedManager
    {
        private readonly RoleSeeder _roleSeeder;
        private readonly AdminSeeder _adminSeeder;

        public SeedManager(
            RoleSeeder roleSeeder,
            AdminSeeder adminSeeder)
        {
            _roleSeeder = roleSeeder;
            _adminSeeder = adminSeeder;
        }

        public async Task Seed()
        {
            await _roleSeeder.SeedRolesAsync();
            await _adminSeeder.SeedAdminAsync();
        }
    }
}