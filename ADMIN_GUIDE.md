# Admin and Staff Guide

## Sign in

Run the application and open <http://localhost:5000>. Use the development Admin account:

- Email: `admin@laundry.local`
- Password: `Admin@123456`

After login, choose **Admin dashboard** or visit <http://localhost:5000/Admin>.

The seeded Staff account is `staff@laundry.local` with password `Staff@123456`. Staff sign in at the same page and use <http://localhost:5000/Staff>.

## Admin dashboard

The Admin dashboard shows total and active orders, non-cancelled booked revenue, customer and staff counts, active service count, monthly revenue for the last six months, and order totals by status. Revenue is based on order totals and is not a payment report.

The **Order queue** link opens the staff workflow. Admin users can process orders there; Staff users cannot open Admin pages.

## Manage accounts

Open **Users & staff** from the Admin dashboard:

- **Create staff ID** creates an Identity account with the Staff role. Set a temporary password of at least six characters and share it with the staff member securely.
- The account table can change non-admin users between Customer and Staff.
- **Deactivate** disables sign-in without deleting the account or its order history. **Reactivate** restores access.
- Admin accounts are intentionally excluded from role changes and deactivation in this screen.

Public registration always creates a Customer account; it cannot grant Staff or Admin privileges.

## Manage services

Open **Service catalog** to add services, edit names/descriptions/prices, or activate/deactivate an entry. Only active entries appear on the customer booking form. Prices are loaded and calculated on the server; submitted form data cannot override catalog prices. Existing orders retain their original service name and price.

## Export data

The Admin dashboard provides CSV downloads for:

- Orders, including customer, pickup, totals, status, and item summaries.
- Users, including assigned roles.
- Services, including price and active status.

## Local database and startup

The app uses SQLite in `laundry.db`. On startup it creates the service table if it is missing and seeds default services only when the catalog is empty. It does not delete the database to update this table. Back up `laundry.db` before manual changes or deployment.

Run with:

```powershell
dotnet run --urls http://localhost:5000
```

The default Admin and Staff passwords are development credentials in `Data/DbSeeder.cs`; change them before deploying outside a local development environment.