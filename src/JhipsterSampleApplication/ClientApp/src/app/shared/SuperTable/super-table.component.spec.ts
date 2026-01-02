import { TestBed } from '@angular/core/testing';

import { SuperTable } from './super-table.component';
import { ColumnConfig } from './super-table.component';

describe('SuperTable', () => {
  let component: SuperTable;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [SuperTable],
    })
      .overrideTemplate(SuperTable, '')
      .compileComponents();

    const fixture = TestBed.createComponent(SuperTable);
    component = fixture.componentInstance;
  });

  it('wraps a row with the first link annotation', () => {
    const col: ColumnConfig = {
      field: 'name',
      header: 'Name',
      annotations: [{ type: 'link', render: row => ({ link: `/detail/${row.id}` }) }],
    };

    expect(component.linkWrap({ id: 7 }, col)).toBe('/detail/7');
  });

  it('returns null when no link annotation is available', () => {
    const col: ColumnConfig = { field: 'name', header: 'Name' };

    expect(component.linkWrap({ id: 1 }, col)).toBeNull();
  });
});
