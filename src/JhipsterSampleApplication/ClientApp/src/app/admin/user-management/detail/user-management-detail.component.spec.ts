import { TestBed } from '@angular/core/testing';

import { Authority } from 'app/config/authority.constants';

import UserManagementDetailComponent from './user-management-detail.component';

describe('User Management Detail Component', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [UserManagementDetailComponent],
    })
      .overrideTemplate(UserManagementDetailComponent, '')
      .compileComponents();
  });

  describe('Construct', () => {
    it('Should accept provided user input', () => {
      // GIVEN
      const fixture = TestBed.createComponent(UserManagementDetailComponent);
      const expectedUser = {
        id: 123,
        login: 'user',
        firstName: 'first',
        lastName: 'last',
        email: 'first@last.com',
        activated: true,
        langKey: 'en',
        authorities: [Authority.USER],
        createdBy: 'admin',
      };

      // WHEN
      fixture.componentRef.setInput('user', expectedUser);
      fixture.detectChanges();

      // THEN
      expect(fixture.componentInstance.user()).toEqual(expect.objectContaining(expectedUser));
    });
  });
});
