import { Injectable, inject } from '@angular/core';
import { Resolve, ActivatedRouteSnapshot, RouterStateSnapshot } from '@angular/router';
import { Observable, of } from 'rxjs';
import { filter, map } from 'rxjs/operators';
import { HttpResponse } from '@angular/common/http';
import { BirthdayService } from '../service/birthday.service';
import { IBirthday, Birthday } from '../birthday.model';

@Injectable({ providedIn: 'root' })
export class BirthdayResolve implements Resolve<IBirthday> {
  private service = inject(BirthdayService);

  resolve(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<IBirthday> {
    const id = route.params['id'];
    if (id) {
      return this.service.find(id).pipe(
        filter((response: HttpResponse<IBirthday>) => response.ok),
        map((birthday: HttpResponse<IBirthday>) => birthday.body!),
      );
    }
    return of(new Birthday());
  }
}
