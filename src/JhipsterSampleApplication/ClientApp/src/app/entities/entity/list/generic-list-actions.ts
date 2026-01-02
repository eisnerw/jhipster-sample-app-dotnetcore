import { InjectionToken, Optional, Inject, Injectable } from '@angular/core';

// Shared row shape for generic list menu handling.
export type GenericListRow = { id?: string; [k: string]: any };

export interface GenericListActionContext {
  entity: string;
  actionKey: string;
  rawRow: GenericListRow | null;
  resolvedRow: GenericListRow | null;
  isChipMenu: boolean;
  selection: GenericListRow[];
  chipMenuRow: GenericListRow | null;
  contextSelectedRow: GenericListRow | null;
  contextSelectedLabel: string | undefined;
  getContextLabel: () => string | undefined;
  setContextLabel: (label: string | undefined) => void;
  helpers: {
    deleteFromContext: () => void;
    openCategorizeDialog: () => void;
    viewIframeFromContext: () => void;
    editFromContext: () => void;
    hasAnyMenuSelection: (row: GenericListRow | null) => boolean;
  };
}

export interface GenericListAction {
  key: string;
  run: (ctx: GenericListActionContext) => void;
  isEnabled?: (ctx: GenericListActionContext) => boolean;
  entities?: string[] | ((entity: string) => boolean);
  priority?: number;
}

export const GENERIC_LIST_ACTIONS = new InjectionToken<GenericListAction[]>('GENERIC_LIST_ACTIONS');

@Injectable({ providedIn: 'root' })
export class GenericListActionResolver {
  constructor(@Optional() @Inject(GENERIC_LIST_ACTIONS) private actions: ReadonlyArray<GenericListAction> | null) {}

  resolve(entity: string, actionKey: string): GenericListAction | null {
    const normalizedKey = actionKey.toLowerCase();
    const candidates = (this.actions || []).filter(a => a.key.toLowerCase() === normalizedKey && this.matchesEntity(a, entity));
    if (!candidates.length) return null;
    const sorted = [...candidates].sort((a, b) => (b.priority ?? 0) - (a.priority ?? 0));
    return sorted[0];
  }

  private matchesEntity(action: GenericListAction, entity: string): boolean {
    if (!action.entities) return true;
    if (Array.isArray(action.entities)) {
      return action.entities.map(e => e.toLowerCase()).includes(entity.toLowerCase());
    }
    return action.entities(entity);
  }
}
