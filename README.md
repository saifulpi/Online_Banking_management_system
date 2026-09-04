# Online Banking Management System

A secure, user-friendly web-based banking platform for managing accounts, performing deposits, withdrawals, transfers, and tracking transaction history — built with ASP.NET Core MVC and MongoDB.

The system provides a complete customer-facing banking experience along with a dedicated admin panel, allowing administrators to manage users, accounts, and monitor all banking activity in real time.

---

## Features

### Customer Portal
- **User Registration & Login** — Sign up with an 8-digit account number, mobile number, email, and password; log in with mobile number and password.
- **Secure Authentication** — Custom claims-based cookie authentication, ASP.NET password hashing, role-based access control, and anti-forgery protection on every form.
- **Re-authentication for Money Movements** — Regular users must re-enter their password to authorize a deposit, withdrawal, or transfer, providing an extra layer of security on sensitive transactions.
- **User Dashboard** — Personalized overview showing current balance, total deposits, total withdrawals, recent activity, and latest transactions.
- **Profile Management** — Update personal details such as name, mobile number, and address.
- **Profile Picture Upload** — Set or remove an optional avatar (JPG, JPEG, PNG, or WEBP, up to 2 MB), shown in the sidebar and displayed immediately after saving.
- **Deposit Money** — Add funds to an account with automatic balance and transaction updates.
- **Withdraw Money** — Withdraw cash with balance validation and configurable per-transaction and daily limits.
- **Money Transfer** — Transfer funds between accounts, with optional reference notes and transactional integrity.
- **Transaction History** — View a chronological, filtered history of deposits, withdrawals, and transfers.
- **Account Details** — View account-level information and running balance.
- **Forgot Password** — Request a password reset by entering your registered email **and** mobile number; a one-time password (OTP) is emailed via Gmail SMTP, and the new password is set after OTP verification (with rules enforced live on the reset page).
- **Interactive Dashboard Cards** — Summary cards (Current Balance, Total Deposit, Total Withdraw, Recent Activity) have a subtle smooth hover zoom + elevation effect that returns to normal on mouse-out without affecting layout.
- **Contact Us** — A public, professional contact page with clickable email (`mailto:`) and phone (`tel:`) links, support hours, and social-media placeholders; linked from the login page and the user dashboard.

### Admin Panel
- **Admin Dashboard** — Aggregate statistics: total users, accounts, deposits, withdrawals, transferred funds, and system-wide balance.
- **User Management** — Search and paginate users, view details, edit profile, and activate or deactivate accounts.
- **Account Management** — Open new accounts, view account details, and freeze or activate accounts.
- **Transaction Oversight** — Browse deposits, withdrawals, transfers, and all transactions with filtering by type, status, account, keyword, and date range.
- **Transaction Details** — Inspect individual transaction records with related account and running balance.

### Platform
- **Validation & Error Handling** — Server-side model validation plus friendly user-facing error messages.
- **Business Rules** — Configurable transaction limits (per-transaction withdrawal, per-transaction transfer, daily withdrawal amount, daily withdrawal count, and daily transfer count).
- **Localized Currency** — All monetary amounts are displayed in Bangladeshi Taka (৳) with a `bn-BD` culture configured app-wide as the default.
- **Responsive UI** — Modern interface built with Tailwind CSS that adapts across desktop and mobile.

---

## Screenshots

### Login
![Login Page](screenshots/login.png)

### User Dashboard
![User Dashboard](screenshots/dashboard.png)

### Profile
![Profile Page](screenshots/profile.png)

### Deposit
![Deposit Page](screenshots/deposit.png)

### Money Transfer
![Money Transfer](screenshots/transfer.png)

### Transaction History
![Transaction History](screenshots/transactions.png)

### Admin Dashboard
![Admin Dashboard](screenshots/admin-dashboard.png)

---

## Tech Stack

| Layer        | Technology                                        |
| ------------ | ------------------------------------------------- |
| Framework    | ASP.NET Core MVC (C#, .NET 10, Razor views)       |
| Database     | MongoDB (MongoDB.Driver 3.11.1) via MongoDB Atlas |
| Frontend     | Tailwind CSS (CDN), Bootstrap, jQuery + jQuery Validation |
| Authentication | Custom claims-based cookie authentication with role authorization |
| Email / OTP    | Gmail SMTP (System.Net.Mail) + in-memory 6-digit OTP service for password reset |
| Security     | Password hashing, anti-forgery tokens, input validation |

---

## Architecture

The application follows the standard ASP.NET Core MVC request pipeline:

- **Controllers** orchestrate requests and enforce authorization:
  - `AuthController` — registration, login, logout, access-denied handling, and the forgot-password / password-reset (OTP) flow.
  - `HomeController` — routes users/admins to their respective dashboards, and serves the public, anonymous `Contact` page.
  - `AccountController` — deposits, withdrawals, transfers, history, and account details.
  - `ProfileController` — profile editing and profile-picture upload/retrieval.
  - `AdminController` — admin dashboard, user management, account management, and transaction oversight.
- **Services** encapsulate business logic and data access:
  - `IUserService` — user registration, authentication, profile management, email-based lookup, and password updates.
  - `IAccountService` — account lifecycle, deposits, withdrawals, transfers (with transactional guarantees), and reporting.
  - `IEmailService` — sends emails (e.g. password-reset OTP) through Gmail SMTP; credentials come from environment variables, never source code.
  - `OtpService` — generates and verifies time-limited, single-use 6-digit OTPs (5-minute expiry, attempt-lockout) via in-memory cache.
- **Models** — domain entities (`AppUser`, `Account`, `Transaction`) plus dedicated view models for each screen.
- **Views** — Razor views organized by controller under `Views/`, sharing layouts, partials, and validation scripts.

### Database Collections

Data is stored in a single MongoDB database (`onlinebank`) across three collections:

| Collection | Purpose                                                                 |
| ---------- | ----------------------------------------------------------------------- |
| `Users`    | Registered users, login credentials, roles, profile data                 |
| `Accounts` | Banking accounts, holder name, phone, balance, type, and status          |
| `Transactions` | Immutable ledger of deposits, withdrawals, and transfers (with running balance and related account) |

A default administrator account is seeded automatically on first startup.

### Transaction Integrity

Money transfers update both the sender's and receiver's balances together. Where the MongoDB deployment supports it, the operation runs inside a **multi-document transaction**; on lower tiers that do not support transactions, the code falls back to a safe non-transactional path so the feature remains available.

---

## Getting Started

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
- Access to a [MongoDB](https://www.mongodb.com/) instance (local or Atlas)

### Setup

1. **Clone the repository**

   ```bash
   git clone https://github.com/your-username/online-banking-management-system.git
   cd online-banking-management-system
   ```

2. **Configure the database connection**

   The connection string, database name, and banking limits are read from `appsettings.json`. Update the `MongoDbSettings` section with your own MongoDB connection string (do not commit real credentials):

   ```json
   "MongoDbSettings": {
     "ConnectionString": "mongodb+srv://<user>:<password>@<cluster>.mongodb.net/?appName=<name>",
     "DatabaseName": "onlinebank"
   }
   ```

   In a deployment environment you can override any of these values with environment variables using the `__` convention (these take precedence over `appsettings.json`):

   ```bash
   MongoDbSettings__ConnectionString=mongodb+srv://<user>:<password>@<cluster>.mongodb.net/
   MongoDbSettings__DatabaseName=onlinebank
   Bankingsettings__MinimumTransactionAmount=100
   ```

3. **Configure email for the password-reset OTP**

   The forgot-password flow sends the reset OTP by email over Gmail SMTP. Provide the sender credentials as environment variables (SMTP settings are read from configuration at startup and credentials are **not** committed to source):

   ```bash
   EmailSettings__Host=smtp.resend.com
   EmailSettings__Port=587
   EmailSettings__Username=resend
   EmailSettings__AppPassword=your-resend-api-key
   EmailSettings__FromEmail=onboarding@resend.dev
   EmailSettings__FromName=Online Bank
   ```

   > Use a [Gmail app password](https://support.google.com/accounts/answer/185833) rather than your normal account password for a more secure setup.

4. **Banking limits** — configurable under `BankingSettings`:
   - `WithdrawLimit` — maximum single-withdrawal amount.
   - `TransferLimit` — maximum single-transfer amount.
   - `DailyWithdrawLimit` — maximum total withdrawals per account per day.
   - `DailyWithdrawCountLimit` — maximum number of withdrawals per account per day.
   - `DailyTransferCountLimit` — maximum number of transfers per account per day.

5. **Run the application**

   ```bash
   dotnet run
   ```

   The application is available at `http://localhost:5070` (see `Properties/launchSettings.json` for the HTTPS profile).

### Default Administrator

An admin account is created automatically on startup. Use it to access the admin panel:

| Field           | Default value    |
| --------------- | ---------------- |
| Mobile number   | `01700000000`    |
| Password        | `Admin@123`      |

> **Security note:** Change the default administrator password immediately after your first login, and use your own database credentials rather than committing them to version control.

---

## Deployment (Railway)

The repository ships with a production-ready `Dockerfile`, a `railway.json` deploy configuration, and a `.dockerignore`, so deploying to [Railway](https://railway.com) is straightforward.

### 1. Prepare your MongoDB

The app reads its connection settings from configuration at startup. For production, provide them via Railway environment variables (these override `appsettings.json`):

| Variable                                  | Description                        |
| ----------------------------------------- | ---------------------------------- |
| `MongoDbSettings__ConnectionString`       | Your MongoDB Atlas connection string |
| `MongoDbSettings__DatabaseName`           | Database name (default `onlinebank`) |
| `BankingSettings__MinimumTransactionAmount` | Optional: override the minimum deposit/withdraw/transfer amount |
| `EmailSettings__Host`                 | SMTP host (Resend: `smtp.resend.com`) |
| `EmailSettings__Port`                 | SMTP port (Resend: `587`) |
| `EmailSettings__Username`             | SMTP username (Resend: `resend`) |
| `EmailSettings__AppPassword`          | SMTP password / API key (use a Resend API key) |
| `EmailSettings__FromEmail`            | Sender address (use `onboarding@resend.dev` for testing; verify a domain for others) |
| `EmailSettings__FromName`             | Sender display name (e.g. `Online Bank`) |

> Use a dedicated production database/credentials rather than committed credentials.

### 2. Push the code to a Git repository

Railway builds directly from a Git remote (GitHub/GitLab/Bitbucket) or a public/private repository.

```bash
git init
git add .
git commit -m "Initial commit"
```

### 3. Create a Railway project

1. Go to [railway.com](https://railway.com) and sign in.
2. Click **New Project** → **Deploy from GitHub repo** and select this repository.
3. Railway automatically detects the `Dockerfile` and builds the image.
4. Add the **`MongoDbSettings__ConnectionString`** (and any other optional vars) in **Variables** for the service.

### 4. Persist profile pictures (Railway Volume)

Profile pictures are written to disk, so they must be stored on a **persistent volume** to survive redeploys/restarts (Railway containers are otherwise ephemeral).

1. In the Railway dashboard, open your service → **Settings** → **Volumes** → **Add Volume**.
2. Set the **Mount Path** to `/data`.
3. Deploy/redeploy the service.

The app automatically detects the mount (via Railway's `RAILWAY_VOLUME_MOUNT_PATH` variable) and stores uploads under `<mount>/profile-pictures`. If you prefer a custom path, set the `PROFILE_PICTURE_PATH` variable explicitly instead.

### 5. Verify

Railway assigns a public URL (e.g. `https://your-app.up.railway.app`). The admin account is seeded automatically on first startup; you can then log in, change the default admin password, and start using the app.

### Notes & limitations

- **Ports:** The `railway.json`/`Dockerfile` bind the app to `0.0.0.0:$PORT` (Railway sets `PORT` automatically). No manual port configuration is needed.
- **HTTPS:** `UseForwardedHeaders` is configured so that HTTPS redirection and HSTS work correctly behind Railway's TLS-terminating proxy without redirect loops.
- **Profile pictures:** Stored on a persistent Railway Volume (`/data/profile-pictures` by default) once a volume is attached, so they survive redeploys. Without a volume attached they fall back to the container's ephemeral filesystem and will be lost on restart.

---

## Project Structure

```
OnlineBankingSystem/
├── Controllers/          # MVC controllers (Auth, Home, Account, Profile, Admin)
├── Data/                 # MongoDbContext, IUserService/UserService, IAccountService/AccountService, EmailService/EmailSettings, OtpService
├── Models/               # Domain entities and view models
├── Validation/           # Custom validation attributes (e.g. profile picture upload)
├── Views/                # Razor views (per-controller folders + shared layouts/partials)
├── Properties/           # launchSettings.json
├── wwwroot/              # Static assets (CSS, JS, libs)
├── appsettings.json      # Configuration (DB connection, banking limits)
├── Program.cs            # Application entry point and service configuration
├── Dockerfile            # Production build/run image (used by Railway)
├── railway.json          # Railway deploy configuration
├── .dockerignore         # Excludes build artifacts from the Docker image
└── OnlineBankingSystem.csproj
```

---

## Security Considerations

- Passwords are stored as hashes using `PasswordHasher<AppUser>`; they are never stored in plain text.
- All restricted controllers and actions enforce authorization via `[Authorize]` and role-based policies (`Admin`).
- Users can only view or transact on their **own** accounts; cross-account access is denied.
- Deposits, withdrawals, and transfers require regular (non-admin) users to re-enter their password for authorization before the transaction is executed.
- All state-changing forms use anti-forgery tokens to protect against CSRF.
- Profile-picture uploads are validated (extension and size), stored with unique filenames, and served only to the owning user (path-traversal and cross-account access are blocked).
- Business limits (per-transaction and daily) are enforced server-side, not just in the UI.
- The forgot-password flow requires both the registered **email and mobile number** to match the same account before an OTP is sent, and it returns a generic message regardless of whether the account exists to avoid user enumeration. OTPs are single-use, expire after 5 minutes, and lock out after repeated failed attempts driven by server-side business logic.
- The `Contact` page is explicitly `[AllowAnonymous]`, so it is reachable from the login page and the user dashboard without exposing any other internal routes.

---

## Contributing

Contributions are welcome. Please open an issue to discuss proposed changes, then submit a pull request.

---

## License

This project is provided for educational purposes. See the `LICENSE` file for details (if applicable).
