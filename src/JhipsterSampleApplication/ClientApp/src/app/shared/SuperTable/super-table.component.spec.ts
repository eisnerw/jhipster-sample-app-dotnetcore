import { TestBed } from '@angular/core/testing';

import { SuperTable } from './super-table.component';
import { ColumnConfig } from './super-table.component';
import { GroupDescriptor } from './super-table.component';

describe('SuperTable', () => {
  let component: SuperTable;
  const buildGroups = (count: number, isGroup = true): GroupDescriptor[] =>
    Array.from({ length: count }, (_, index) => ({
      name: `Group ${index}`,
      count: index + 1,
      isGroup,
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

  it('does not use the custom viewport for a large multilevel top-level group table', () => {
    component.mode = 'group';
    component.scrollHeight = 'flex';
    component.superTableParent = null;
    component.groups = buildGroups(1000);
    component['syncGroupViewportState']();

    expect(component.useCustomGroupVirtualViewport).toBe(false);
  });

  it('disables virtual scroll for nested group tables', () => {
    component.mode = 'group';
    component.scrollHeight = 'flex';
    component.superTableParent = {} as SuperTable;
    component.groups = buildGroups(1000);
    component['syncGroupViewportState']();

    expect(component.useCustomGroupVirtualViewport).toBe(false);
  });

  it('disables the custom viewport for smaller group tables', () => {
    component.mode = 'group';
    component.scrollHeight = 'flex';
    component.superTableParent = null;
    component.groups = buildGroups(499);
    component['syncGroupViewportState']();

    expect(component.useCustomGroupVirtualViewport).toBe(false);
  });

  it('uses the custom viewport for a large top-level leaf group table', () => {
    component.mode = 'group';
    component.scrollHeight = 'flex';
    component.superTableParent = null;
    component.groups = buildGroups(500, false);
    component['syncGroupViewportState']();

    expect(component.useCustomGroupVirtualViewport).toBe(true);
  });

  it('keeps multilevel grouped tables on the p-table path', () => {
    component.mode = 'group';
    component.scrollHeight = 'flex';
    component.superTableParent = null;
    component.groups = buildGroups(500, true);
    component['syncGroupViewportState']();

    expect(component.useCustomGroupVirtualViewport).toBe(false);
  });
});
