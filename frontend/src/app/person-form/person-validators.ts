import { AbstractControl, ValidationErrors } from '@angular/forms';

export function requiredText(control: AbstractControl): ValidationErrors | null {
  return typeof control.value === 'string' && control.value.trim() ? null : { required: true };
}

/** Parse explicitly in Gregorian order; never use locale-dependent Date parsing. */
export function toApiBirthDate(value: string): string | null {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);
  if (!match) return null;
  const [, day, month, year] = match;
  if (+year < 1) return null;
  const iso = `${year}-${month}-${day}`;
  const date = new Date(`${iso}T00:00:00Z`);
  if (!Number.isFinite(date.getTime()) || date.toISOString().slice(0, 10) !== iso) return null;
  return iso;
}

export function birthDateValidator(control: AbstractControl): ValidationErrors | null {
  if (!control.value) return null; // Required validator owns empty values.
  const iso = toApiBirthDate(control.value);
  if (!iso) return { birthDate: true };
  return iso > new Date().toISOString().slice(0, 10) ? { futureDate: true } : null;
}
