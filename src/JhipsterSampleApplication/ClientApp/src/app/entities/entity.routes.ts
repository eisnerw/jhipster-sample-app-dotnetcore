import { Routes } from '@angular/router';

const routes: Routes = [
  {
    path: 'movie',
    data: { pageTitle: 'Movies' },
    loadChildren: () => import('./movie/movie.routes'),
  },
  {
    path: 'birthday',
    data: { pageTitle: 'Birthdays' },
    loadChildren: () => import('./birthday/birthday.routes'),
  },
  {
    path: 'supreme',
    data: { pageTitle: 'Supreme' },
    loadChildren: () => import('./supreme/supreme.routes'),
  },
  {
    path: 'named-query',
    data: { pageTitle: 'JobHistories' },
    loadChildren: () => import('./named-query/named-query-routing.module').then(m => m.NamedQueryRoutingModule),
  },
  {
    path: 'selector',
    data: { pageTitle: 'Selectors' },
    loadChildren: () => import('./selector/selector.routes'),
  },
  {
    path: 'view',
    data: { pageTitle: 'Views' },
    loadChildren: () => import('./view/view.routes').then(m => m.VIEW_ROUTE),
  },
  {
    path: 'history',
    data: { pageTitle: 'Histories' },
    loadChildren: () => import('./history/history.routes'),
  },
  /* jhipster-needle-add-entity-route - JHipster will add entity modules routes here */
];

export default routes;
