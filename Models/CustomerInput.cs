#nullable enable
using System;
using System.ComponentModel.DataAnnotations;

namespace retailCLDVportal.Models
{
 
    /// MVC input/view model for Create/Edit forms.
    /// Keeps UI validation concerns out of the storage entity.
   
    public class CustomerInput
    {
        [Required, StringLength(50)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string Surname { get; set; } = string.Empty;

        [DataType(DataType.Date)]
        [Display(Name = "Date of Birth")]
        public DateTime? DateOfBirth { get; set; }

        [Phone]
        [Display(Name = "Phone Number")]
        public string? PhoneNumber { get; set; }

        [EmailAddress]
        public string? Email { get; set; }

        [Display(Name = "Company Name")]
        [StringLength(100)]
        public string? CompanyName { get; set; }

        [Display(Name = "Address Line 1")]
        [StringLength(120)]
        public string? AddressLine1 { get; set; }

        [Display(Name = "Address Line 2")]
        [StringLength(120)]
        public string? AddressLine2 { get; set; }

        [StringLength(60)]
        public string? City { get; set; }

        [StringLength(60)]
        public string? State { get; set; }

        [Display(Name = "Zip Code")]
        [StringLength(20)]
        public string? ZipCode { get; set; }

        [StringLength(60)]
        public string? Country { get; set; }
    }
}
