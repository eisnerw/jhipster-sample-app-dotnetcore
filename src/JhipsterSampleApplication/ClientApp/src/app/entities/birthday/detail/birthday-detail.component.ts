import { Component, OnInit } from '@angular/core';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { CommonModule } from '@angular/common';

import { IBirthday } from '../birthday.model';
import SharedModule from 'app/shared/shared.module';

@Component({
  selector: 'jhi-birthday-detail',
  templateUrl: './birthday-detail.component.html',
  standalone: true,
  imports: [SharedModule, RouterModule, CommonModule],
})
export class BirthdayDetailComponent implements OnInit {
  birthday: IBirthday | null = null;

  constructor(protected activatedRoute: ActivatedRoute) {}

  ngOnInit(): void {
    this.activatedRoute.data.subscribe(({ birthday }) => {
      this.birthday = birthday;
    });
  }

  previousState(): void {
    window.history.back();
  }
}
