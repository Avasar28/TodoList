using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TodoListApp.Models
{
    public class UserWebAuthnCredential
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        [Required]
        public byte[] CredentialId { get; set; } = Array.Empty<byte>();

        [Required]
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();

        [Required]
        public uint SignCount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Metadata for device/user-friendly name
        public string? DeviceName { get; set; }
    }
}
