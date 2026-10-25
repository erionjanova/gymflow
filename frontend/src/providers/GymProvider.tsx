import { createContext, useContext, type ReactNode } from "react";
import type { Gym, TenantRole } from "@/lib/types";

export interface GymContextValue {
  gym: Gym;
  slug: string;
  role: TenantRole | null;
  isOwner: boolean;
  isStaff: boolean;
  isMember: boolean;
}

const GymContext = createContext<GymContextValue | null>(null);

export function GymProvider({ gym, role, children }: { gym: Gym; role: TenantRole | null; children: ReactNode }) {
  const value: GymContextValue = {
    gym,
    slug: gym.slug,
    role,
    isOwner: role === "Owner",
    isStaff: role === "Owner" || role === "Trainer",
    isMember: role !== null,
  };

  return <GymContext.Provider value={value}>{children}</GymContext.Provider>;
}

export function useGymContext() {
  const ctx = useContext(GymContext);
  if (!ctx) throw new Error("useGymContext must be used inside GymProvider");
  return ctx;
}
