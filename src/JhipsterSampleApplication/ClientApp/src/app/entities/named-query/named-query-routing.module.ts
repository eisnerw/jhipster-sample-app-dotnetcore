import { NgModule } from '@angular/core';
import { RouterModule } from '@angular/router';

import { NAMED_QUERY_ROUTE } from './named-query.routes';

@NgModule({
  imports: [RouterModule.forChild(NAMED_QUERY_ROUTE)],
  exports: [RouterModule],
})
export class NamedQueryRoutingModule {}
