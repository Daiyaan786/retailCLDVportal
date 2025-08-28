#nullable enable
using System;
using System.ComponentModel.DataAnnotations;
using Azure;
using Azure.Data.Tables;

namespace retailCLDVportal.Models
{
    public sealed class CustomerEntity : ITableEntity
    {
        // Table keys
        public string PartitionKey { get; set; } = default!;
        public string RowKey { get; set; } = default!;
        public ETag ETag { get; set; } = ETag.All;
        public DateTimeOffset? Timestamp { get; set; }

        // Domain
        [Required, StringLength(50)]
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

        [Display(Name = "Company Name"), StringLength(100)]
        public string? CompanyName { get; set; }

        [Display(Name = "Address Line 1"), StringLength(120)]
        public string? AddressLine1 { get; set; }

        [Display(Name = "Address Line 2"), StringLength(120)]
        public string? AddressLine2 { get; set; }

        [StringLength(60)]
        public string? City { get; set; }

        [StringLength(60)]
        public string? State { get; set; }

        [Display(Name = "Zip Code"), StringLength(20)]
        public string? ZipCode { get; set; }

        [StringLength(60)]
        public string? Country { get; set; }

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        // ---- Helpers ----
        public static string ComputePartitionKey(string surname)
        {
            if (string.IsNullOrWhiteSpace(surname)) return "_";
            var c = char.ToUpperInvariant(surname.Trim()[0]);
            return (c is >= 'A' and <= 'Z') ? c.ToString() : "_";
        }

        private static DateTime? NormalizeToUtc(DateTime? value)
        {
            if (!value.HasValue) return null;
            var dt = value.Value;
            return dt.Kind switch
            {
                DateTimeKind.Utc => dt,
                DateTimeKind.Local => dt.ToUniversalTime(),
                _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc) // Unspecified -> mark as UTC
            };
        }

        public static CustomerEntity NewFrom(CustomerInput input)
        {
            return new CustomerEntity
            {
                PartitionKey = ComputePartitionKey(input.Surname ?? string.Empty),
                RowKey = Guid.NewGuid().ToString("N"),
                FirstName = input.FirstName?.Trim() ?? string.Empty,
                Surname = input.Surname?.Trim() ?? string.Empty,
                DateOfBirth = NormalizeToUtc(input.DateOfBirth),
                PhoneNumber = input.PhoneNumber?.Trim(),
                Email = input.Email?.Trim(),
                CompanyName = string.IsNullOrWhiteSpace(input.CompanyName) ? null : input.CompanyName!.Trim(),
                AddressLine1 = input.AddressLine1?.Trim(),
                AddressLine2 = input.AddressLine2?.Trim(),
                City = input.City?.Trim(),
                State = input.State?.Trim(),
                ZipCode = input.ZipCode?.Trim(),
                Country = input.Country?.Trim(),
                CreatedAtUtc = DateTime.UtcNow
            };
        }

        public void UpdateFrom(CustomerInput input)
        {
            FirstName = input.FirstName?.Trim() ?? FirstName;
            Surname = input.Surname?.Trim() ?? Surname;
            DateOfBirth = NormalizeToUtc(input.DateOfBirth);
            PhoneNumber = input.PhoneNumber?.Trim();
            Email = input.Email?.Trim();
            CompanyName = string.IsNullOrWhiteSpace(input.CompanyName) ? null : input.CompanyName!.Trim();
            AddressLine1 = input.AddressLine1?.Trim();
            AddressLine2 = input.AddressLine2?.Trim();
            City = input.City?.Trim();
            State = input.State?.Trim();
            ZipCode = input.ZipCode?.Trim();
            Country = input.Country?.Trim();
            // Optionally re-partition if surname changed:
            // PartitionKey = ComputePartitionKey(Surname);
        }
    }
}

