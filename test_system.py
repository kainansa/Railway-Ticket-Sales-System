import requests
import re
from datetime import datetime, timedelta

BASE_URL = "http://localhost:5121"

def get_csrf_token(session, url):
    r = session.get(url)
    match = re.search(r'name="__RequestVerificationToken"\s+type="hidden"\s+value="([^"]+)"', r.text)
    if match:
        return match.group(1)
    match = re.search(r'value="([^"]+)"\s+name="__RequestVerificationToken"', r.text)
    return match.group(1) if match else None

def test_suite():
    session = requests.Session()
    results = []

    def log(test_name, success, detail=""):
        status = "PASS" if success else "FAIL"
        print(f"[{status}] {test_name}: {detail}")
        results.append((test_name, success, detail))

    # Test 1: Home Page
    r = session.get(f"{BASE_URL}/")
    log("T01_HomePage", r.status_code == 200 and "VIETNAM RAILWAYS" in r.text, f"Status {r.status_code}")

    # Test 2: Refund Policy Page
    r = session.get(f"{BASE_URL}/Home/RefundPolicy")
    log("T02_RefundPolicy", r.status_code == 200 and "Cancellation Fee" in r.text, f"Status {r.status_code}")

    # Test 3: Passenger Login
    token = get_csrf_token(session, f"{BASE_URL}/Account/Login")
    login_data = {
        "__RequestVerificationToken": token,
        "Email": "passenger@railway.com",
        "Password": "Passenger@123",
        "RememberMe": "false"
    }
    r = session.post(f"{BASE_URL}/Account/Login", data=login_data, allow_redirects=True)
    is_logged_in = "Nguyen Van An" in r.text or "passenger@railway.com" in r.text or "My Bookings" in r.text
    log("T03_PassengerLogin", is_logged_in, "Passenger authenticated successfully")

    # Test 4: Passenger Access Denied on Admin
    r = session.get(f"{BASE_URL}/Admin")
    log("T04_PassengerRoleRestriction", r.status_code in [403, 302] or "Access Restricted" in r.text, f"Admin access blocked for passenger (Status {r.status_code})")

    # Test 5: Search Trains
    tomorrow = (datetime.now() + timedelta(days=1)).strftime("%Y-%m-%d")
    r = session.get(f"{BASE_URL}/Train?departureStationId=1&arrivalStationId=8&travelDate={tomorrow}")
    has_results = "Found" in r.text and "SE1" in r.text
    log("T05_TrainSearch", has_results, f"Search returned train schedules for {tomorrow}")

    # Test 6: View Seat Map & Availability
    # Extract scheduleId from search results
    m = re.search(r'/Booking/SelectSeat\?scheduleId=(\d+)', r.text)
    schedule_id = m.group(1) if m else "1"
    r = session.get(f"{BASE_URL}/Booking/SelectSeat?scheduleId={schedule_id}")
    has_seatmap = "Select Coach & Seat" in r.text and "seat-box" in r.text
    log("T06_SeatSelectionMap", has_seatmap, f"Seat map rendered for Schedule {schedule_id}")

    # Test 7: Enter Passenger Info
    r = session.get(f"{BASE_URL}/Booking/PassengerInfo?scheduleId={schedule_id}&seatIds=15")
    has_pax_form = "Passenger & Contact Details" in r.text
    log("T07_PassengerInfoForm", has_pax_form, "Passenger details form prepared")

    # Test 8: Create Booking with 10-Minute Hold (BR-01, BR-03, BR-04)
    token = get_csrf_token(session, f"{BASE_URL}/Booking/PassengerInfo?scheduleId={schedule_id}&seatIds=15")
    booking_data = {
        "__RequestVerificationToken": token,
        "ScheduleId": schedule_id,
        "CustomerName": "Nguyen Van An",
        "CustomerEmail": "passenger@railway.com",
        "CustomerPhone": "0987654321",
        "CustomerIdCard": "001201000123",
        "Passengers[0].SeatId": "15",
        "Passengers[0].SeatNumber": "1-15",
        "Passengers[0].CoachNumber": "1",
        "Passengers[0].Price": "650000",
        "Passengers[0].PassengerName": "Nguyen Van An",
        "Passengers[0].PassengerIdCard": "001201000123",
        "Passengers[0].PassengerType": "Adult"
    }
    r = session.post(f"{BASE_URL}/Booking/Create", data=booking_data, allow_redirects=True)
    has_payment_page = "10-Minute Hold Reservation" in r.text or "Payment" in r.text
    order_id_match = re.search(r'name="bookingOrderId"\s+value="(\d+)"', r.text)
    booking_order_id = order_id_match.group(1) if order_id_match else None
    log("T08_CreateBooking_10MinHold", has_payment_page and booking_order_id is not None, f"10-min hold created for Order ID {booking_order_id}")

    # Test 9: Process Successful Payment & E-Ticket Generation (BR-05)
    token = get_csrf_token(session, f"{BASE_URL}/Booking/Payment/{booking_order_id}")
    pay_data = {
        "__RequestVerificationToken": token,
        "bookingOrderId": booking_order_id,
        "paymentMethod": "OnlineBanking",
        "simulatedOutcome": "Success"
    }
    r = session.post(f"{BASE_URL}/Booking/ProcessPayment", data=pay_data, allow_redirects=True)
    has_confirm = "Booking Confirmed & Paid!" in r.text and "TKT-" in r.text
    log("T09_PaymentSuccess_ETicketIssued", has_confirm, "Payment successful and E-Tickets issued")

    # Extract ticket ID
    m_tkt = re.search(r'/Ticket/Details/(\d+)', r.text)
    new_ticket_id = m_tkt.group(1) if m_tkt else "1"

    # Test 10: View E-Ticket with QR Code
    r = session.get(f"{BASE_URL}/Ticket/Details/{new_ticket_id}")
    has_qr = "data:image/png;base64," in r.text and "ELECTRONIC BOARDING PASS" in r.text
    log("T10_ETicket_QRCode_Display", has_qr, f"E-ticket {new_ticket_id} rendered with 2D QR Code")

    # Test 11: My Bookings
    r = session.get(f"{BASE_URL}/Ticket/MyBookings")
    has_bookings = "My Bookings" in r.text and "Completed / Paid" in r.text
    log("T11_MyBookingsList", has_bookings, "Booking history accessible to passenger")

    # Test 12: Ticket Cancellation (BR-07, BR-09)
    # Review cancellation
    r = session.get(f"{BASE_URL}/Ticket/Cancel/{new_ticket_id}")
    has_cancel_preview = "Ticket Cancellation & Refund Calculation" in r.text and "Refundable Amount" in r.text
    token = get_csrf_token(session, f"{BASE_URL}/Ticket/Cancel/{new_ticket_id}")
    r = session.post(f"{BASE_URL}/Ticket/ConfirmCancel", data={"__RequestVerificationToken": token, "ticketId": new_ticket_id}, allow_redirects=True)
    has_cancelled = "Ticket cancelled successfully" in r.text or "Request Refund" in r.text
    log("T12_TicketCancellation", has_cancelled, f"Ticket {new_ticket_id} cancelled and seat released")

    # Test 13: Request Refund (BR-09, BR-10)
    token = get_csrf_token(session, f"{BASE_URL}/Refund/RequestRefund?ticketId={new_ticket_id}")
    refund_data = {
        "__RequestVerificationToken": token,
        "ticketId": new_ticket_id,
        "reason": "Change of travel plans",
        "simulatedOutcome": "Success"
    }
    r = session.post(f"{BASE_URL}/Refund/ProcessRefund", data=refund_data, allow_redirects=True)
    has_refund = "Refund History" in r.text or "processed successfully" in r.text
    log("T13_ProcessRefund", has_refund, f"Refund completed for ticket {new_ticket_id}")

    # Test 14: Prevent Duplicate Refund (BR-10)
    r = session.get(f"{BASE_URL}/Refund/RequestRefund?ticketId={new_ticket_id}")
    is_duplicate_blocked = "already been refunded" in r.text or "Refund Ineligible" in r.text
    log("T14_PreventDuplicateRefund", is_duplicate_blocked, "Duplicate refund prevented (BR-10)")

    # Test 15: Clerk Login & Counter Operations
    session_clerk = requests.Session()
    token = get_csrf_token(session_clerk, f"{BASE_URL}/Account/Login")
    r = session_clerk.post(f"{BASE_URL}/Account/Login", data={
        "__RequestVerificationToken": token,
        "Email": "clerk@railway.com",
        "Password": "Clerk@123",
        "RememberMe": "false"
    }, allow_redirects=True)
    print("Clerk final URL:", r.url, "History:", [h.url for h in r.history])
    is_clerk_in = r.url.endswith("/Clerk") or "/Clerk" in r.url or "Station Ticket Counter Portal" in r.text
    log("T15_ClerkLogin", is_clerk_in, "Ticket clerk authenticated")

    # Test 16: Clerk Manage Bookings Search
    r = session_clerk.get(f"{BASE_URL}/Clerk/ManageBookings")
    log("T16_ClerkManageBookings", "Booking Management" in r.text, "Clerk booking search working")

    # Test 17: Admin Login & Management
    session_admin = requests.Session()
    token = get_csrf_token(session_admin, f"{BASE_URL}/Account/Login")
    r = session_admin.post(f"{BASE_URL}/Account/Login", data={
        "__RequestVerificationToken": token,
        "Email": "admin@railway.com",
        "Password": "Admin@123",
        "RememberMe": "false"
    }, allow_redirects=True)
    is_admin_in = "Railway System Administration" in r.text or "Admin Dashboard" in r.text
    log("T17_AdminLogin", is_admin_in, "Administrator authenticated")

    # Test 18: Admin Trains Management
    r = session_admin.get(f"{BASE_URL}/Admin/Trains")
    log("T18_AdminTrains", "Train Fleet Management" in r.text and "SE1" in r.text, "Admin train list loaded")

    # Test 19: Admin Schedules Management
    r = session_admin.get(f"{BASE_URL}/Admin/Schedules")
    log("T19_AdminSchedules", "Train Schedules" in r.text, "Admin schedules loaded")

    # Test 20: Admin Reports & System Monitor
    r_rep = session_admin.get(f"{BASE_URL}/Admin/BookingReports")
    r_mon = session_admin.get(f"{BASE_URL}/Admin/SystemMonitor")
    log("T20_AdminReportsAndMonitor", r_rep.status_code == 200 and r_mon.status_code == 200, "Analytics reports & Live System Monitor accessible")

    total_passed = sum(1 for _, ok, _ in results if ok)
    print(f"\nSummary: {total_passed}/{len(results)} tests passed.")
    return total_passed == len(results)

if __name__ == "__main__":
    test_suite()
