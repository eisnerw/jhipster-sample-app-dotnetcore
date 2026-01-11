import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { DialogModule } from 'primeng/dialog';

import SharedModule from 'app/shared/shared.module';
import HasAnyAuthorityDirective from 'app/shared/auth/has-any-authority.directive';
import { AccountService } from 'app/core/auth/account.service';
import { LoginService } from 'app/login/login.service';
import { ProfileService } from 'app/layouts/profiles/profile.service';
import { EntityGenericService } from 'app/entities/entity/service/entity-generic.service';
import { environment } from 'environments/environment';
import NavbarItem from './navbar-item.model';
import { PrimeThemeKey, PrimeThemeService } from 'app/shared/theme/prime-theme.service';

@Component({
  selector: 'jhi-navbar',
  templateUrl: './navbar.component.html',
  styleUrl: './navbar.component.scss',
  imports: [RouterModule, FormsModule, SharedModule, HasAnyAuthorityDirective, DialogModule],
})
export default class NavbarComponent implements OnInit {
  inProduction?: boolean;
  isNavbarCollapsed = signal(true);
  openAPIEnabled?: boolean;
  version = '';
  account = inject(AccountService).trackCurrentAccount();
  entitiesNavbarItems: NavbarItem[] = [];

  private readonly loginService = inject(LoginService);
  private readonly profileService = inject(ProfileService);
  private readonly router = inject(Router);
  private readonly entityService = inject(EntityGenericService);
  private readonly themeService = inject(PrimeThemeService);
  showThemeDialog = false;
  themeOptions = this.themeService.themeOptions;
  selectedThemeKey: PrimeThemeKey = this.themeService.getThemeKey();
  selectedDarkMode = this.themeService.isDarkMode();
  selectedInvertGrid = this.themeService.isInvertGrid();
  private originalThemeKey: PrimeThemeKey = this.selectedThemeKey;
  private originalDarkMode = this.selectedDarkMode;
  private originalInvertGrid = this.selectedInvertGrid;
  private themeDialogCommitted = false;

  constructor() {
    const { VERSION } = environment;
    if (VERSION) {
      this.version = VERSION.toLowerCase().startsWith('v') ? VERSION : `v${VERSION}`;
    }
  }

  ngOnInit(): void {
    this.entityService.listEntities().subscribe(res => {
      const list = res.body ?? [];
      this.entitiesNavbarItems = list.map(e => ({
        name: e.title || e.name,
        route: `/entity/${encodeURIComponent(e.name)}`,
        translationKey: `global.menu.entities.${e.name}`,
      }));
    });
    this.profileService.getProfileInfo().subscribe(profileInfo => {
      this.inProduction = profileInfo.inProduction;
      this.openAPIEnabled = profileInfo.openAPIEnabled;
    });
  }

  collapseNavbar(): void {
    this.isNavbarCollapsed.set(true);
  }

  login(): void {
    this.router.navigate(['/login']);
  }

  logout(): void {
    this.collapseNavbar();
    this.loginService.logout();
    this.router.navigate(['']);
  }

  toggleNavbar(): void {
    this.isNavbarCollapsed.update(isNavbarCollapsed => !isNavbarCollapsed);
  }

  openThemeDialog(): void {
    this.selectedThemeKey = this.themeService.getThemeKey();
    this.selectedDarkMode = this.themeService.isDarkMode();
    this.selectedInvertGrid = this.themeService.isInvertGrid();
    this.originalThemeKey = this.selectedThemeKey;
    this.originalDarkMode = this.selectedDarkMode;
    this.originalInvertGrid = this.selectedInvertGrid;
    this.themeDialogCommitted = false;
    this.showThemeDialog = true;
  }

  applyThemeSelection(closeDialog = true): void {
    this.themeService.setThemeSelection(this.selectedThemeKey, this.selectedDarkMode, this.selectedInvertGrid);
    if (closeDialog) {
      this.themeDialogCommitted = true;
      this.showThemeDialog = false;
    }
  }

  cancelThemeSelection(): void {
    this.themeService.setThemeSelection(this.originalThemeKey, this.originalDarkMode, this.originalInvertGrid);
    this.showThemeDialog = false;
  }

  onThemeDialogHide(): void {
    if (!this.themeDialogCommitted) {
      this.themeService.setThemeSelection(this.originalThemeKey, this.originalDarkMode, this.originalInvertGrid);
    }
  }
}
