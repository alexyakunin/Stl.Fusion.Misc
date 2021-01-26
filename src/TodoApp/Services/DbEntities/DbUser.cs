using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Stl;

namespace TodoApp.Services
{
    [Index(nameof(Name), IsUnique = true)]
    [Index(nameof(Email), IsUnique = true)]
    public class DbUser : IHasId<string>
    {
        [Key]
        public string Id { get; set; } = "";
        public string AuthenticationType { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";
        public string ClaimsJson { get; set; } = "";
    }
}
