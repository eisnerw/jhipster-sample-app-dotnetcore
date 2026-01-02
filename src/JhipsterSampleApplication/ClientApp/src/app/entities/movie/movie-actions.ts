import { GenericListAction, GENERIC_LIST_ACTIONS } from '../entity/list/generic-list-actions';

const MOVIE_GENERIC_LIST_ACTIONS: GenericListAction[] = [
  {
    key: 'view',
    entities: ['movie'],
    run: ctx => ctx.helpers.viewIframeFromContext(),
    isEnabled: ctx => !!ctx.resolvedRow,
    priority: 1,
  },
];

export const MOVIE_GENERIC_LIST_ACTION_PROVIDERS = MOVIE_GENERIC_LIST_ACTIONS.map(action => ({
  provide: GENERIC_LIST_ACTIONS,
  useValue: action,
  multi: true,
}));
