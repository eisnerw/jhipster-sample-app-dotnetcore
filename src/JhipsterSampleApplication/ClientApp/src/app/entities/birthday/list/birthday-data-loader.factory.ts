import { Injectable } from '@angular/core';
import { BirthdayService } from 'app/entities/birthday/service/birthday.service';
import { BirthdayDataLoader } from './birthday-data-loader';

@Injectable({ providedIn: 'root' })
export class BirthdayDataLoaderFactory {
  constructor(private birthdayService: BirthdayService) {}

  create(): BirthdayDataLoader {
    return new BirthdayDataLoader(this.birthdayService);
  }
}
