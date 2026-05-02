namespace RentVibe.Models.Enums;

public enum UserRole
{
    Tenant,
    Landlord,
    Admin
}

public enum AccountStatus
{
    Pending,
    Approved,
    Rejected
}

public enum ApprovalStatus
{
    Pending,
    Approved,
    Rejected
}

public enum RentalStatus
{
    Available,
    Rented
}

public enum PropertyType
{
    Apartment,
    House,
    Condo,
    Studio,
    Villa,
    Townhouse,
    Other
}

public enum VisitStatus
{
    Pending,
    Accepted,
    Rejected,
    Cancelled
}

public enum ApplicationStatus
{
    Pending,
    Accepted,
    Rejected
}

public enum NotificationType
{
    VisitRequested,
    VisitAccepted,
    VisitRejected,
    ApplicationSubmitted,
    ApplicationAccepted,
    ApplicationRejected,
    PropertyApproved,
    PropertyRejected,
    AccountApproved,
    AccountRejected,
    NewReview
}
