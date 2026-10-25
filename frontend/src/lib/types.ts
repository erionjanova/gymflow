export type Language = "sq" | "en";
export type TenantRole = "Member" | "Trainer" | "Owner";
export type BillingInterval = "Month" | "Year";
export type BookingStatus = "Booked" | "Waitlisted" | "Cancelled" | "Attended" | "NoShow";
export type InvitationStatus = "Pending" | "Accepted" | "Revoked" | "Expired";
export type SubscriptionStatus = "Incomplete" | "Active" | "PastDue" | "Canceled";
export type DayOfWeek = "Sunday" | "Monday" | "Tuesday" | "Wednesday" | "Thursday" | "Friday" | "Saturday";

export interface User {
  id: string;
  fullName: string;
  email: string;
  phoneNumber: string | null;
  preferredLanguage: Language;
  isPlatformAdmin: boolean;
}

export interface AuthResponse {
  token: string;
  expiresAt: string;
  user: User;
}

export interface MyGym {
  id: string;
  name: string;
  slug: string;
  brandColor: string;
  role: TenantRole;
}

export interface Gym {
  id: string;
  name: string;
  slug: string;
  description: string | null;
  address: string | null;
  phone: string | null;
  brandColor: string;
  currency: string;
  timeZone: string;
  requireActiveMembership: boolean;
}

export interface GymMembership {
  gym: Gym;
  role: TenantRole | null;
}

export interface Plan {
  id: string;
  name: string;
  description: string | null;
  price: number;
  interval: BillingInterval;
  isActive: boolean;
  sortOrder: number;
}

export interface GymClass {
  id: string;
  name: string;
  description: string | null;
  durationMinutes: number;
  defaultCapacity: number;
  isActive: boolean;
}

export interface Session {
  id: string;
  classId: string;
  className: string;
  trainerId: string | null;
  trainerName: string | null;
  startsAt: string;
  endsAt: string;
  capacity: number;
  bookedCount: number;
  spotsLeft: number;
  waitlistCount: number;
  isCancelled: boolean;
  myStatus: BookingStatus | null;
  myWaitlistPosition: number | null;
}

export interface ScheduleResult {
  created: number;
  skipped: number;
  sessions: Session[];
}

export interface RosterEntry {
  bookingId: string;
  userId: string;
  fullName: string;
  email: string;
  status: BookingStatus;
  bookedAt: string;
  waitlistPosition: number | null;
}

export interface Booking {
  id: string;
  sessionId: string;
  className: string;
  trainerName: string | null;
  startsAt: string;
  endsAt: string;
  status: BookingStatus;
  waitlistPosition: number | null;
  isSessionCancelled: boolean;
  bookedAt: string;
}

export interface Member {
  userId: string;
  fullName: string;
  email: string;
  phoneNumber: string | null;
  role: TenantRole;
  joinedAt: string;
}

export interface Invitation {
  id: string;
  email: string;
  role: TenantRole;
  status: InvitationStatus;
  invitedBy: string;
  createdAt: string;
  expiresAt: string;
}

export interface InvitationPreview {
  gymName: string;
  gymSlug: string;
  brandColor: string;
  email: string;
  role: TenantRole;
  status: InvitationStatus;
  hasAccount: boolean;
}

export interface AcceptInvitationResponse {
  gym: MyGym;
  auth: AuthResponse | null;
}

export interface MySubscription {
  id: string;
  planId: string;
  planName: string;
  price: number;
  interval: BillingInterval;
  status: SubscriptionStatus;
  currentPeriodEnd: string | null;
  cancelAtPeriodEnd: boolean;
}

export interface GymSubscription {
  id: string;
  userId: string;
  memberName: string;
  memberEmail: string;
  planName: string;
  price: number;
  interval: BillingInterval;
  status: SubscriptionStatus;
  currentPeriodEnd: string | null;
  cancelAtPeriodEnd: boolean;
  createdAt: string;
}

export interface BillingOverview {
  activeMembers: number;
  monthlyRecurringRevenue: number;
  currency: string;
  subscriptions: GymSubscription[];
}

export interface PlatformGym {
  id: string;
  name: string;
  slug: string;
  isSuspended: boolean;
  createdAt: string;
  memberCount: number;
  ownerEmail: string | null;
}

export interface PlatformStats {
  gyms: number;
  activeGyms: number;
  users: number;
  memberships: number;
}
