import { Routes } from '@angular/router';

const routes: Routes = [
  {
    path: 'entity',
    data: { pageTitle: 'Entity' },
    loadChildren: () => import('./entity/entity.routes'),
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
    path: 'history',
    data: { pageTitle: 'Histories' },
    loadChildren: () => import('./history/history.routes'),
  },
  /* jhipster-needle-add-entity-route - JHipster will add entity modules routes here */
];

export default routes;
