import { DOCUMENT, isPlatformBrowser } from '@angular/common';
import { inject, Injectable, PLATFORM_ID, signal } from '@angular/core';
import { PrimeNG } from 'primeng/config';
import { Theme, definePreset, palette } from '@primeuix/styled';
import Aura from '@primeng/themes/aura';
import Lara from '@primeng/themes/lara';
import Material from '@primeng/themes/material';
import Nora from '@primeng/themes/nora';

export type PrimeThemeKey = 'aura' | 'lara' | 'material' | 'nora';

type PrimeThemeOption = {
  key: PrimeThemeKey;
  label: string;
  preset: unknown;
};

const THEME_STORAGE_KEY = 'primeng.theme';
const DARK_STORAGE_KEY = 'primeng.dark';
const DARK_MODE_CLASS = 'prime-theme-dark';
const INVERT_STORAGE_KEY = 'primeng.invertGrid';
const INVERT_MODE_CLASS = 'prime-theme-invert-grid';

const withPrimaryPalette = (preset: unknown, color: string): unknown =>
  definePreset(preset as Record<string, unknown>, {
    semantic: {
      primary: palette(color),
    },
  });

const THEME_OPTIONS: PrimeThemeOption[] = [
  { key: 'aura', label: 'Aura', preset: Aura },
  { key: 'lara', label: 'Lara', preset: withPrimaryPalette(Lara, '#3b82f6') },
  { key: 'material', label: 'Material', preset: withPrimaryPalette(Material, '#7c3aed') },
  { key: 'nora', label: 'Nora', preset: withPrimaryPalette(Nora, '#f97316') },
];

@Injectable({ providedIn: 'root' })
export class PrimeThemeService {
  private readonly primeNg = inject(PrimeNG);
  private readonly document = inject(DOCUMENT);
  private readonly platformId = inject(PLATFORM_ID);

  private readonly themeKey = signal<PrimeThemeKey>('aura');
  private readonly darkMode = signal<boolean>(false);
  private readonly invertGrid = signal<boolean>(false);

  readonly themeOptions = THEME_OPTIONS.map(option => ({ key: option.key, label: option.label }));

  initialize(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    const storedTheme = window.localStorage.getItem(THEME_STORAGE_KEY) as PrimeThemeKey | null;
    const storedDark = window.localStorage.getItem(DARK_STORAGE_KEY);
    const storedInvert = window.localStorage.getItem(INVERT_STORAGE_KEY);

    if (storedTheme && this.isThemeKey(storedTheme)) {
      this.themeKey.set(storedTheme);
    }
    if (storedDark !== null) {
      this.darkMode.set(storedDark === 'true');
    }
    if (storedInvert !== null) {
      this.invertGrid.set(storedInvert === 'true');
    }

    this.applyTheme(this.themeKey(), this.darkMode());
    this.setInvertGridClass(this.invertGrid());
  }

  getThemeKey(): PrimeThemeKey {
    return this.themeKey();
  }

  isDarkMode(): boolean {
    return this.darkMode();
  }

  isInvertGrid(): boolean {
    return this.invertGrid();
  }

  setThemeSelection(themeKey: PrimeThemeKey, darkMode: boolean, invertGrid?: boolean): void {
    this.themeKey.set(themeKey);
    this.darkMode.set(darkMode);
    if (invertGrid !== undefined) {
      this.invertGrid.set(invertGrid);
      this.setInvertGridClass(invertGrid);
    }
    this.persistSelection(themeKey, darkMode, this.invertGrid());
    this.applyTheme(themeKey, darkMode);
  }

  private applyTheme(themeKey: PrimeThemeKey, darkMode: boolean): void {
    const preset = THEME_OPTIONS.find(option => option.key === themeKey)?.preset ?? Aura;
    const themeConfig = {
      preset,
      options: {
        darkModeSelector: `.${DARK_MODE_CLASS}`,
      },
    };
    this.primeNg.setConfig({ theme: themeConfig });
    Theme.setTheme(themeConfig);
    this.setDarkModeClass(darkMode);
  }

  private setDarkModeClass(enabled: boolean): void {
    const root = this.document.documentElement;
    root.classList.toggle(DARK_MODE_CLASS, enabled);
  }

  private setInvertGridClass(enabled: boolean): void {
    const root = this.document.documentElement;
    root.classList.toggle(INVERT_MODE_CLASS, enabled);
  }

  private persistSelection(themeKey: PrimeThemeKey, darkMode: boolean, invertGrid: boolean): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }

    window.localStorage.setItem(THEME_STORAGE_KEY, themeKey);
    window.localStorage.setItem(DARK_STORAGE_KEY, String(darkMode));
    window.localStorage.setItem(INVERT_STORAGE_KEY, String(invertGrid));
  }

  private isThemeKey(value: string): value is PrimeThemeKey {
    return THEME_OPTIONS.some(option => option.key === value);
  }
}
