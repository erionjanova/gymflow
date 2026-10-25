import { lazy, Suspense } from "react";
import { BrowserRouter, Route, Routes } from "react-router";
import { RootLayout } from "@/components/layout/RootLayout";
import {
  FullPageSpinner,
  GuestOnly,
  RequireAuth,
  RequireGymMember,
  RequireGymOwner,
  RequireGymStaff,
  RequirePlatformAdmin,
} from "@/components/layout/RouteGuards";
import GymLayout from "@/components/gym/GymLayout";
import LoginPage from "@/pages/auth/LoginPage";
import RegisterPage from "@/pages/auth/RegisterPage";
import CreateGymPage from "@/pages/CreateGymPage";
import GymHomePage from "@/pages/gym/GymHomePage";
import MembershipPage from "@/pages/gym/MembershipPage";
import MyBookingsPage from "@/pages/gym/MyBookingsPage";
import SchedulePage from "@/pages/gym/SchedulePage";
import InvitePage from "@/pages/InvitePage";
import LandingPage from "@/pages/LandingPage";
import MyGymsPage from "@/pages/MyGymsPage";
import NotFoundPage from "@/pages/NotFoundPage";

const PlatformPage = lazy(() => import("@/pages/PlatformPage"));
const AdminLayout = lazy(() => import("@/pages/gym/admin/AdminLayout"));
const DashboardPage = lazy(() => import("@/pages/gym/admin/DashboardPage"));
const SessionsPage = lazy(() => import("@/pages/gym/admin/SessionsPage"));
const ClassesPage = lazy(() => import("@/pages/gym/admin/ClassesPage"));
const PlansPage = lazy(() => import("@/pages/gym/admin/PlansPage"));
const MembersPage = lazy(() => import("@/pages/gym/admin/MembersPage"));
const SettingsPage = lazy(() => import("@/pages/gym/admin/SettingsPage"));

export default function App() {
  return (
    <BrowserRouter>
      <Suspense fallback={<FullPageSpinner />}>
        <Routes>
          <Route element={<RootLayout />}>
            <Route index element={<LandingPage />} />
            <Route path="invite/:token" element={<InvitePage />} />

            <Route element={<GuestOnly />}>
              <Route path="login" element={<LoginPage />} />
              <Route path="register" element={<RegisterPage />} />
            </Route>

            <Route element={<RequireAuth />}>
              <Route path="gyms" element={<MyGymsPage />} />
              <Route path="gyms/new" element={<CreateGymPage />} />
            </Route>

            <Route element={<RequirePlatformAdmin />}>
              <Route path="platform" element={<PlatformPage />} />
            </Route>

            <Route path="g/:slug" element={<GymLayout />}>
              <Route index element={<GymHomePage />} />
              <Route path="schedule" element={<SchedulePage />} />
              <Route element={<RequireGymMember />}>
                <Route path="bookings" element={<MyBookingsPage />} />
                <Route path="membership" element={<MembershipPage />} />
                <Route element={<RequireGymStaff />}>
                  <Route path="admin" element={<AdminLayout />}>
                    <Route element={<RequireGymOwner />}>
                      <Route index element={<DashboardPage />} />
                      <Route path="classes" element={<ClassesPage />} />
                      <Route path="plans" element={<PlansPage />} />
                      <Route path="settings" element={<SettingsPage />} />
                    </Route>
                    <Route path="sessions" element={<SessionsPage />} />
                    <Route path="members" element={<MembersPage />} />
                  </Route>
                </Route>
              </Route>
            </Route>

            <Route path="*" element={<NotFoundPage />} />
          </Route>
        </Routes>
      </Suspense>
    </BrowserRouter>
  );
}
