import { Injectable } from '@angular/core';

export const MAX_IMAGE_BYTES = 2 * 1024 * 1024;
export const MAX_IMAGE_PIXELS = 16_000_000;

export interface ProfileImage {
  base64: string;
  fileName: string;
  contentType: string;
  preview: string;
}

@Injectable({ providedIn: 'root' })
export class ProfileImageService {
  async read(file: File): Promise<ProfileImage> {
    const extension = file.name.split('.').pop()?.toLowerCase();
    const contentType =
      extension === 'png'
        ? 'image/png'
        : extension === 'jpg' || extension === 'jpeg'
          ? 'image/jpeg'
          : '';
    if (!contentType || (file.type && file.type !== contentType)) {
      throw new Error('Please select a JPG or PNG image with a matching file type.');
    }
    if (file.name.length > 255) throw new Error('Profile filename must not exceed 255 characters.');
    if (!file.size || file.size > MAX_IMAGE_BYTES)
      throw new Error('Profile must be an image no larger than 2 MB.');

    const preview = await new Promise<string>((resolve, reject) => {
      const reader = new FileReader();
      reader.onload = () =>
        typeof reader.result === 'string'
          ? resolve(reader.result)
          : reject(new Error('Unable to read the profile image.'));
      reader.onerror = reader.onabort = () =>
        reject(new Error('Unable to read the profile image. Please select it again.'));
      reader.readAsDataURL(file);
    });
    const base64 = preview.slice(preview.indexOf(',') + 1);
    const bytes = atob(base64);
    const signature =
      contentType === 'image/png'
        ? bytes.startsWith('\x89PNG\r\n\x1a\n')
        : bytes.startsWith('\xff\xd8\xff');
    if (!signature) throw new Error('Profile must contain a valid JPG or PNG image.');

    await new Promise<void>((resolve, reject) => {
      const image = new Image();
      image.onload = () => {
        const pixels = image.naturalWidth * image.naturalHeight;
        if (pixels <= 0 || pixels > MAX_IMAGE_PIXELS) {
          reject(new Error('Profile must not exceed 16 megapixels (16,000,000 pixels).'));
        } else resolve();
      };
      image.onerror = () => reject(new Error('Profile image is damaged or cannot be opened.'));
      image.src = `data:${contentType};base64,${base64}`;
    });
    return {
      base64,
      fileName: file.name,
      contentType,
      preview: `data:${contentType};base64,${base64}`,
    };
  }
}
