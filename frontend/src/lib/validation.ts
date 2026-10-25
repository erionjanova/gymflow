import { z } from "zod";

export const emailSchema = z.string().trim().min(1, "validation.required").email("validation.email");

export const nameSchema = z.string().trim().min(1, "validation.required").max(100, "validation.tooLong");

export const optionalPhoneSchema = z
  .string()
  .trim()
  .regex(/^(\+?[0-9\s\-()]{6,20})?$/, "validation.phone")
  .optional();

export const passwordSchema = z
  .string()
  .min(8, "validation.passwordMin")
  .max(100, "validation.tooLong")
  .regex(/[A-Za-z]/, "validation.passwordLetter")
  .regex(/[0-9]/, "validation.passwordNumber");

export const loginSchema = z.object({
  email: emailSchema,
  password: z.string().min(1, "validation.required"),
});

export const registerSchema = z.object({
  fullName: nameSchema,
  email: emailSchema,
  password: passwordSchema,
});

export const newAccountSchema = z.object({
  fullName: nameSchema,
  password: passwordSchema,
});

export const slugSchema = z
  .string()
  .trim()
  .regex(/^([a-z0-9]+(-[a-z0-9]+)*)?$/, "validation.slug")
  .refine((s) => s === "" || (s.length >= 3 && s.length <= 40), "validation.slug")
  .optional();

export const createGymSchema = z.object({
  name: nameSchema,
  slug: slugSchema,
  address: z.string().trim().max(200, "validation.tooLong").optional(),
  phone: optionalPhoneSchema,
  currency: z.enum(["EUR", "USD", "GBP", "CHF"]),
});

export const gymSettingsSchema = z.object({
  name: nameSchema,
  description: z.string().trim().max(1000, "validation.tooLong").optional(),
  address: z.string().trim().max(200, "validation.tooLong").optional(),
  phone: optionalPhoneSchema,
  brandColor: z.string().regex(/^#[0-9a-fA-F]{6}$/, "validation.color"),
  currency: z.enum(["EUR", "USD", "GBP", "CHF"]),
  timeZone: z.string().min(1, "validation.required"),
  requireActiveMembership: z.boolean(),
});

export const planSchema = z.object({
  name: nameSchema,
  description: z.string().trim().max(500, "validation.tooLong").optional(),
  price: z.number({ error: "validation.number" }).min(0, "validation.price").max(10000, "validation.price"),
  interval: z.enum(["Month", "Year"]),
  isActive: z.boolean(),
  sortOrder: z.number({ error: "validation.number" }).int().min(0),
});

export const classSchema = z.object({
  name: nameSchema,
  description: z.string().trim().max(500, "validation.tooLong").optional(),
  durationMinutes: z.number({ error: "validation.number" }).int().min(10, "validation.duration").max(240, "validation.duration"),
  defaultCapacity: z.number({ error: "validation.number" }).int().min(1, "validation.capacity").max(500, "validation.capacity"),
  isActive: z.boolean(),
});

export const inviteSchema = z.object({
  email: emailSchema,
  role: z.enum(["Trainer", "Owner"]),
});
