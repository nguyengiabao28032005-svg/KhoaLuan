using System.ComponentModel.DataAnnotations;

namespace BeautyShop.ViewModels
{
    public class EditProfileViewModel
    {
        [Required]
        public string FullName { get; set; } = "";


    [Required]
        public string Email { get; set; } = "";

        public string? PhoneNumber { get; set; }
    }


}
