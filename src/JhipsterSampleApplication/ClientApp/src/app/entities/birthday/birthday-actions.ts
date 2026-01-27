import { GenericListAction, GENERIC_LIST_ACTIONS, GenericListActionContext, GenericListRow } from '../entity/list/generic-list-actions';

const resolveRow = (ctx: GenericListActionContext): GenericListRow | null =>
  ctx.resolvedRow || ctx.contextSelectedRow || ctx.selection?.[0] || ctx.rawRow || ctx.chipMenuRow || null;

const resolveAlternateRow = (ctx: GenericListActionContext): GenericListRow | null => ctx.resolveAlternateChipRow || null;

const canViewBirthdayRow = (row: GenericListRow | null): boolean => {
  if (!row) return false;
  if (typeof row.lname === 'string' && row.lname.toLowerCase() === 'johnson') return false;
  return true;
};

const canDoIt = (row: GenericListRow | null): boolean => {
  if (!row) return false;
  if (typeof row.lname === 'string' && row.lname.toLowerCase() === 'smith') return false;
  return true;
};

const canRelate = (row: GenericListRow | null, alternateRow: GenericListRow | null): boolean => {
  if (!row || !alternateRow) return false;
  if ((row.id ?? row) === (alternateRow.id ?? alternateRow)) return false;
  if (typeof row.lname === 'string' && row.lname.toLowerCase() === 'smith') return false;
  return true;
};

const markBirthday = (ctx: GenericListActionContext): void => {
  const row = resolveRow(ctx);
  const id = row?.id;
  if (!id) return;
  const label = ctx.contextSelectedLabel ?? ctx.getContextLabel();
  alert(`label=${label} id=${id}`);
};

const relateRows = (ctx: GenericListActionContext): void => {
  const row = resolveRow(ctx);
  const alternateRow = resolveAlternateRow(ctx);
  const id = row?.id;
  const alternateId = alternateRow?.id;
  if (!id || !alternateId) return;
  const label = ctx.contextSelectedLabel ?? ctx.getContextLabel();
  alert(`label=${label} id=${id} alternateId=${alternateId}`);
};

const BIRTHDAY_GENERIC_LIST_ACTIONS: GenericListAction[] = [
  {
    key: 'view',
    entities: ['birthday'],
    run: ctx => ctx.helpers.viewIframeFromContext(),
    isEnabled: ctx => canViewBirthdayRow(resolveRow(ctx)),
    priority: 1,
  },
  {
    key: 'mark',
    entities: ['birthday'],
    run: ctx => markBirthday(ctx),
    isEnabled: ctx => !!resolveRow(ctx),
  },
  {
    key: 'markbirthday',
    entities: ['birthday'],
    run: ctx => markBirthday(ctx),
    isEnabled: ctx => !!resolveRow(ctx),
  },
  {
    key: 'doit',
    entities: ['birthday'],
    // Parent menu group; child actions perform the real work.
    run: () => {
      // no-op; acts as a grouping action
      return;
    },
    isEnabled: ctx => canDoIt(resolveRow(ctx)),
    priority: 1,
  },
  {
    key: 'relate',
    entities: ['birthday'],
    // Parent menu group; child actions perform the real work.
    run: ctx => relateRows(ctx),
    isEnabled: ctx => canRelate(resolveRow(ctx), resolveAlternateRow(ctx)),
    priority: 1,
  },
];

export const BIRTHDAY_GENERIC_LIST_ACTION_PROVIDERS = BIRTHDAY_GENERIC_LIST_ACTIONS.map(action => ({
  provide: GENERIC_LIST_ACTIONS,
  useValue: action,
  multi: true,
}));
