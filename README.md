# Railway Ticket Sales System (Hệ Thống Bán Vé Tàu Hỏa)

A complete, runnable **Railway Ticket Sales System** built with **ASP.NET Core MVC .NET 8**, **C# 12**, **Entity Framework Core 8**, **ASP.NET Core Identity**, and **Bootstrap 5**.

The system strictly adheres to the project requirements and models specified in:
- `PROJECT ASSIGNMENT_LAB.docx` – User Requirement Document (URD)
- `2026-10-28T05_45_31.309Z_SWD.docx` – Analysis Modelling Document

---

## 1. Key Features & Business Workflows

### 1.1 Complete Railway Ticket Lifecycle
1. **Search Trains:** Look up schedules by departure station, destination, and travel date.
2. **Coach & Seat Selection:** Interactive carriage seat grid with live seat status indicators (Available, Temporarily Held, Booked).
3. **10-Minute Seat Reservation Hold (BR-03, BR-04):** Server-side hold mechanism preventing duplicate seat bookings, synchronized with a live countdown timer.
4. **Payment Gateway Integration:** Simulated payment processing (Success, Rejected, Timeout flows) for Online Banking, VietQR, VNPay Mock, and Credit Cards.
5. **Electronic Ticket Issuance (BR-05):** Generates electronic tickets with secure 2D QR codes and boarding pass print styles (`window.print()`).
6. **Ticket Change Workflow (BR-07, BR-08):** Allows changing eligible tickets (&ge; 4 hours before departure) to alternative trains and seats with automated fare difference and exchange fee calculation.
7. **Ticket Cancellation Workflow (BR-07, BR-09, BR-10):** Cancels tickets before departure deadline, calculates cancellation penalty fees, releases the seat back to inventory, and sends cancellation notice.
8. **Automated Refund Workflow (BR-09, BR-10):** Evaluates `RefundPolicy` based on time until departure, transfers refundable amounts via Payment Gateway, and prevents duplicate refund attempts.

---

## 2. User Roles & Portals

### 2.1 Passenger
- Register account, log in/out, edit profile.
- Search trains, view timetables, coach layouts, seat availability and pricing.
- Reserve seats with 10-minute hold, make payments, receive E-tickets with QR codes.
- Manage personal bookings in "My Bookings".
- Perform self-service ticket change, cancellation, and refund requests.

### 2.2 Ticket Clerk
- Station Ticket Counter Portal (`/Clerk`).
- Walk-in passenger train search and seat selection.
- Create counter bookings with direct cash/POS payment confirmation.
- Issue and print multi-ticket counter boarding passes with QR codes.
- Search bookings and tickets by booking code, ticket code, phone number, or ID card.
- Process ticket changes, cancellations, and refunds at the station window.

### 2.3 Administrator
- Full Administration Portal (`/Admin`).
- **Railway Data Management:** CRUD for Trains, Stations, Routes, and Schedules/Trips.
- **Ticket Configuration:** Manage Coaches, auto-generate Seat inventories, and configure Fares & Multipliers.
- **User & Staff Management:** Manage passenger and staff accounts, toggle Active/Locked status, create Ticket Clerk and Administrator accounts.
- **Analytics & Reports:** Booking volume reports, Route revenue reports with net income calculations, printable reports.
- **Live System Monitor:** Real-time diagnostics of active 10-minute seat holds, expiring orders, email/SMS notification delivery logs, and payment transactions.

---

## 3. Technology Stack

- **Framework:** ASP.NET Core MVC .NET 8
- **Language:** C# 12
- **ORM:** Entity Framework Core 8 (Code-First)
- **Database:** SQLite (default for zero-setup execution) & SQL Server ready
- **Authentication:** ASP.NET Core Identity with password hashing
- **Frontend:** Razor Views, HTML5, CSS3, Bootstrap 5, Bootstrap Icons, JavaScript
- **QR Code Generation:** QRCoder (Base64 PNG generation)
- **Architecture:** MVC + Service Layer with Dependency Injection & Repository-like DB Context

---

## 4. Default Demo Accounts

The database is automatically created and seeded on initial launch with pre-hashed accounts:

| Role | Email | Password |
|---|---|---|
| **Administrator** | `admin@railway.com` | `Admin@123` |
| **Ticket Clerk** | `clerk@railway.com` | `Clerk@123` |
| **Passenger** | `passenger@railway.com` | `Passenger@123` |

> *Tip: The Login page contains 1-click credential buttons for instant testing.*

---

## 5. How to Run

### Quick Start (SQLite Mode - Ready to run immediately)
```powershell
# Navigate to project directory
cd RailwayTicketSales

# Run the web application
dotnet run --launch-profile http
```
Open your browser and visit: **`http://localhost:5121`**

### SQL Server Mode
To use Microsoft SQL Server instead of SQLite:
1. Open `RailwayTicketSales/appsettings.json`.
2. Set `"DatabaseProvider": "SqlServer"`.
3. Update `"DefaultConnection"` with your SQL Server connection string.
4. Run migrations:
   ```powershell
   dotnet ef database update
   ```
5. Start the application:
   ```powershell
   dotnet run
   ```

---

## 6. Automated Testing

An end-to-end integration test suite is included in `test_system.py`:
```powershell
# Start the web app in one terminal
cd RailwayTicketSales
dotnet run --launch-profile http

# In another terminal, run test suite
python test_system.py
```
**Test Coverage (20/20 Passed):**
- Authentication & Role-based Access Control
- Schedule search & seat availability
- 10-minute seat hold concurrency (BR-01, BR-02, BR-03, BR-04)
- Payment Gateway simulation & E-ticket QR issuance (BR-05)
- Ticket modification & fare difference (BR-07, BR-08)
- Ticket cancellation & seat release (BR-07, BR-09)
- Refund calculation & duplicate prevention (BR-09, BR-10)
- Ticket Clerk counter workflows
- Administrator management & live system diagnostics
