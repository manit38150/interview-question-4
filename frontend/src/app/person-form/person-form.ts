import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, ElementRef, inject, signal, viewChild } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormControl,
  FormGroup,
  FormGroupDirective,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { PersonApiService } from '../core/person-api.service';
import { ProfileImage, ProfileImageService } from '../core/profile-image.service';
import { birthDateValidator, requiredText, toApiBirthDate } from './person-validators';

type Field =
  'firstName' | 'lastName' | 'email' | 'phone' | 'profile' | 'birthDate' | 'occupation' | 'sex';

@Component({
  selector: 'app-person-form',
  standalone: true,
  imports: [ReactiveFormsModule],
  templateUrl: './person-form.html',
  styleUrl: './person-form.scss',
})
export class PersonForm {
  private readonly api = inject(PersonApiService);
  private readonly images = inject(ProfileImageService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fileInput = viewChild<ElementRef<HTMLInputElement>>('fileInput');
  private readonly formDirective = viewChild(FormGroupDirective);
  private imageVersion = 0;
  private submitted = false;

  private successTimer?: ReturnType<typeof setTimeout>;

  readonly saving = signal(false);
  readonly readingImage = signal(false);
  readonly profile = signal<ProfileImage | null>(null);
  readonly imageError = signal('');
  readonly serverErrors = signal<Partial<Record<Field, string>>>({});
  readonly errorMessage = signal('');
  readonly savedId = signal<number | null>(null);
  readonly occupations = ['Developer', 'Designer', 'Engineer', 'Teacher', 'Student', 'Other'];
  readonly toApiBirthDate = toApiBirthDate;
  readonly today = new Date().toISOString().slice(0, 10);
  readonly form = new FormGroup({
    firstName: new FormControl('', {
      nonNullable: true,
      validators: [requiredText, Validators.maxLength(100)],
    }),
    lastName: new FormControl('', {
      nonNullable: true,
      validators: [requiredText, Validators.maxLength(100)],
    }),
    email: new FormControl('', {
      nonNullable: true,
      validators: [requiredText, Validators.email, Validators.maxLength(254)],
    }),
    phone: new FormControl('', {
      nonNullable: true,
      validators: [requiredText, Validators.pattern(/^0[0-9]{8,9}$/)],
    }),
    profile: new FormControl<ProfileImage | null>(null, Validators.required),
    birthDate: new FormControl('', {
      nonNullable: true,
      validators: [requiredText, birthDateValidator],
    }),
    occupation: new FormControl('', {
      nonNullable: true,
      validators: [requiredText, Validators.maxLength(100)],
    }),
    sex: new FormControl('', {
      nonNullable: true,
      validators: [requiredText, Validators.pattern(/^(Male|Female)$/)],
    }),
  });

  constructor() {
    this.destroyRef.onDestroy(() => ++this.imageVersion);
    for (const field of Object.keys(this.form.controls) as Field[]) {
      const control: AbstractControl = this.form.controls[field];
      control.valueChanges.pipe(takeUntilDestroyed()).subscribe(() => {
        if (this.serverErrors()[field]) {
          this.serverErrors.update((errors) => ({ ...errors, [field]: undefined }));
        }
      });
    }
  }

  message(field: Field): string {
    if (this.serverErrors()[field]) return this.serverErrors()[field]!;
    if (field === 'profile' && this.imageError()) return this.imageError();
    const control = this.form.controls[field];
    if (!(control.touched || this.submitted) || !control.errors) return '';
    const labels: Record<Field, string> = {
      firstName: 'First Name',
      lastName: 'Last Name',
      email: 'Email',
      phone: 'Phone',
      profile: 'profile',
      birthDate: 'Birth Day',
      occupation: 'Occupation',
      sex: 'Sex',
    };
    if (control.hasError('required')) {
      if (field === 'profile' || field === 'occupation')
        return `Please selected any ${labels[field]}`;
      return `Please provide ${labels[field]}`;
    }
    if (field === 'phone') return 'Please provide a valid Phone (9–10 digits, starting with 0)';
    if (control.hasError('maxlength')) return `${labels[field]} is too long.`;
    if (field === 'birthDate')
      return 'Please provide a valid Birth Day (DD/MM/YYYY, not in the future)';
    return `Please provide a valid ${labels[field]}`;
  }

  async selectProfile(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return; // Cancelling the picker preserves the previous selection.
    const version = ++this.imageVersion;
    this.profile.set(null);
    this.form.controls.profile.setValue(null);
    this.form.controls.profile.markAsTouched();
    this.imageError.set('');
    this.savedId.set(null);
    this.readingImage.set(true);
    try {
      const image = await this.images.read(file);
      if (version !== this.imageVersion) return;
      this.profile.set(image);
      this.form.controls.profile.setValue(image);
    } catch (error) {
      if (version !== this.imageVersion) return;
      this.imageError.set(
        error instanceof Error ? error.message : 'Unable to read the profile image.',
      );
      input.value = '';
    } finally {
      if (version === this.imageVersion) this.readingImage.set(false);
    }
  }

  openDatePicker(picker: HTMLInputElement): void {
    if (this.saving()) return;
    try {
      if (typeof picker.showPicker === 'function') {
        // Call synchronously from the user gesture; native pickers require activation.
        picker.showPicker();
        return;
      }
    } catch {
      // Fall back to native activation if the browser does not allow showPicker.
    }
    picker.focus();
    picker.click();
  }

  selectDate(event: Event): void {
    const iso = (event.target as HTMLInputElement).value;
    const [year, month, day] = iso.split('-');
    this.form.controls.birthDate.setValue(iso ? `${day}/${month}/${year}` : '');
    this.form.controls.birthDate.markAsTouched();
  }

  private showSuccess(id: number): void {
    clearTimeout(this.successTimer);

    this.savedId.set(id);

    this.successTimer = setTimeout(() => {
      this.savedId.set(null);
      this.successTimer = undefined;
    }, 3000);
  }

  save(): void {
    if (this.saving() || this.readingImage()) return;
    this.submitted = true;
    this.savedId.set(null);
    this.errorMessage.set('');
    this.serverErrors.set({});
    this.form.markAllAsTouched();
    if (this.form.invalid) return;
    const value = this.form.getRawValue();
    const image = value.profile!;
    this.saving.set(true);
    this.api
      .create({
        firstName: value.firstName.trim(),
        lastName: value.lastName.trim(),
        email: value.email.trim(),
        phone: value.phone,
        profileBase64: image.base64,
        profileFileName: image.fileName,
        profileContentType: image.contentType,
        birthDate: toApiBirthDate(value.birthDate)!,
        occupation: value.occupation,
        sex: value.sex,
      })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (response) => {
          this.saving.set(false);
          if (
            response.status !== 201 ||
            !Number.isSafeInteger(response.body?.id) ||
            response.body!.id <= 0
          ) {
            this.errorMessage.set(
              'The server returned an unexpected response. Your form has been preserved.',
            );
            return;
          }
          const id = response.body!.id;
          this.reset();
          this.showSuccess(id);
        },
        error: (error: HttpErrorResponse) => {
          this.saving.set(false);
          if (error.status === 400) {
            const mapped: Partial<Record<Field, string>> = {};
            const errors: unknown = error.error?.errors;
            if (errors && typeof errors === 'object') {
              for (const [key, messages] of Object.entries(errors)) {
                const normalized = key.split('.').pop()!.toLowerCase();
                const field = normalized.startsWith('profile')
                  ? 'profile'
                  : (Object.keys(this.form.controls) as Field[]).find(
                      (name) => name.toLowerCase() === normalized,
                    );
                if (
                  field &&
                  Array.isArray(messages) &&
                  messages.every((message) => typeof message === 'string')
                ) {
                  mapped[field] = messages.join(' ');
                  this.form.controls[field].markAsTouched();
                }
              }
            }
            this.serverErrors.set(mapped);
            this.errorMessage.set(
              'Please check the highlighted fields and try again. Your form has been preserved.',
            );
          } else {
            this.errorMessage.set(
              error.status === 0
                ? 'Unable to connect to the server. Check your connection and try again. Your form has been preserved.'
                : 'Unable to save data. Please try again later. Your form has been preserved.',
            );
          }
        },
      });
  }

  clear(): void {
    if (!this.saving()) this.reset();
  }

  private reset(): void {
    ++this.imageVersion;
    this.formDirective()?.resetForm();
    this.form.reset();
    const input = this.fileInput()?.nativeElement;
    if (input) input.value = '';
    this.profile.set(null);
    this.imageError.set('');
    this.readingImage.set(false);
    this.serverErrors.set({});
    this.errorMessage.set('');
    this.savedId.set(null);
    this.submitted = false;
  }
}
