import { Injectable } from '@angular/core';

@Injectable({
  providedIn: 'root'
})
export class EncryptionService {

  private secretKey = 'ESL$ecure@Key#2026!XyZ';

  // Encrypt
  encrypt(data: string): string {
    try {
      const mixed = data + '|' + this.secretKey;
      return btoa(unescape(encodeURIComponent(mixed)));
    } catch {
      return data;
    }
  }

  // Decrypt
  decrypt(encryptedData: string): string {
    try {
      const decoded = decodeURIComponent(escape(atob(encryptedData)));
      return decoded.split('|')[0];
    } catch {
      return encryptedData;
    }
  }
}