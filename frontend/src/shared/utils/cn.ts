import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

/** Gộp class Tailwind, lớp sau thắng lớp trước khi xung đột — giống cn() của bản React. */
export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}
