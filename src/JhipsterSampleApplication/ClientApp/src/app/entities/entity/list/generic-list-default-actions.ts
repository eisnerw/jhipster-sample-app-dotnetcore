import { GenericListAction, GENERIC_LIST_ACTIONS } from './generic-list-actions';

const DEFAULT_GENERIC_LIST_ACTIONS: GenericListAction[] = [
  {
    key: 'delete',
    run: ctx => ctx.helpers.deleteFromContext(),
    isEnabled: ctx => !!ctx.resolvedRow,
  },
  {
    key: 'categorize',
    run: ctx => ctx.helpers.openCategorizeDialog(),
    isEnabled: ctx => ctx.helpers.hasAnyMenuSelection(ctx.rawRow),
  },
  {
    key: 'view',
    run: ctx => ctx.helpers.viewIframeFromContext(),
    isEnabled: ctx => !!ctx.resolvedRow,
  },
  {
    key: 'edit',
    run: ctx => ctx.helpers.editFromContext(),
    isEnabled: ctx => !!ctx.resolvedRow,
  },
];

export const DEFAULT_GENERIC_LIST_ACTION_PROVIDERS = DEFAULT_GENERIC_LIST_ACTIONS.map(action => ({
  provide: GENERIC_LIST_ACTIONS,
  useValue: action,
  multi: true,
}));
