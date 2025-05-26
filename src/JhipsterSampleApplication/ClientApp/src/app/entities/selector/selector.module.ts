import { NgModule } from "@angular/core";
import { SharedModule } from "app/shared/shared.module";
import { SelectorComponent } from "./list/selector.component";
import { SelectorDetailComponent } from "./detail/selector-detail.component";
import { SelectorUpdateComponent } from "./update/selector-update.component";
import { SelectorDeleteDialogComponent } from "./delete/selector-delete-dialog.component";
import { SelectorRoutingModule } from "./route/selector-routing.module";

@NgModule({
    imports: [SharedModule, SelectorRoutingModule],
    declarations: [
        SelectorComponent,
        SelectorDetailComponent,
        SelectorUpdateComponent,
        SelectorDeleteDialogComponent,
    ]
})
export class SelectorModule {}
