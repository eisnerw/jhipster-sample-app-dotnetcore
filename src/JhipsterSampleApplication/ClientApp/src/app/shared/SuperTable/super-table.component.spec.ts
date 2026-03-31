import { TestBed } from '@angular/core/testing';

import { SuperTable } from './super-table.component';
import { ColumnConfig } from './super-table.component';
import { GroupDescriptor } from './super-table.component';

describe('SuperTable', () => {
  let component: SuperTable;
  const buildGroups = (count: number): GroupDescriptor[] =>
    Array.from({ length: count }, (_, index) => ({
      name: `Group ${index}`,
      count: index + 1,
      isGroup: true,
    }));

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

  it('uses virtual scroll for a large top-level group table', () => {
    component.mode = 'group';
    component.scrollHeight = 'flex';
    component.superTableParent = null;
    component.groups = buildGroups(1000);
    component['syncGroupVirtualScrollState']();

    expect(component.useGroupVirtualScroll).toBe(true);
  });

  it('disables virtual scroll for nested group tables', () => {
    component.mode = 'group';
    component.scrollHeight = 'flex';
    component.superTableParent = {} as SuperTable;
    component.groups = buildGroups(1000);
    component['syncGroupVirtualScrollState']();

    expect(component.useGroupVirtualScroll).toBe(false);
  });

  it('disables virtual scroll for smaller group tables', () => {
    component.mode = 'group';
    component.scrollHeight = 'flex';
    component.superTableParent = null;
    component.groups = buildGroups(999);
    component['syncGroupVirtualScrollState']();

    expect(component.useGroupVirtualScroll).toBe(false);
  });
});
