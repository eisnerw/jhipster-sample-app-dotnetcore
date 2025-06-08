import { NgModule } from '@angular/core';
import { BirthdayTableComponent } from './birthday-table.component';
import { SuperTableModule } from '../../../shared/SuperTable/super-table';
import { CalendarModule } from 'primeng/calendar';
import { ContextMenuModule } from 'primeng/contextmenu';
import { MessagesModule } from 'primeng/messages';
import { ChipsModule } from 'primeng/chips';
import { ConfirmPopupModule } from "primeng/confirmpopup";
import {TooltipModule} from 'primeng/tooltip';
import {ScrollTopModule} from 'primeng/scrolltop';
import { MenuModule } from 'primeng/menu';
import { DialogModule } from 'primeng/dialog';
import { EditableMultiSelectModule } from '../../../shared/editable-multi-select.module';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@NgModule({
    imports: [CommonModule, SuperTableModule, CalendarModule, ContextMenuModule, MessagesModule, ChipsModule, ConfirmPopupModule, TooltipModule, ScrollTopModule, MenuModule, DialogModule, EditableMultiSelectModule, FormsModule],
    declarations: [BirthdayTableComponent],
    exports: [BirthdayTableComponent]
})
export class BirthdayTableModule {} 