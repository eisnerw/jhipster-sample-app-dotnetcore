import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { convertToParamMap, ActivatedRoute, Router } from '@angular/router';
import { HttpResponse } from '@angular/common/http';
import { of } from 'rxjs';

import { GenericListComponent } from './generic-list.component';
import { EntityGenericService } from '../service/entity-generic.service';
import { ViewService } from '../../view/service/view.service';
import { GenericListActionResolver } from './generic-list-actions';
import { MessageService, ConfirmationService } from 'primeng/api';

describe('GenericListComponent', () => {
  let component: GenericListComponent;
  let fixture: any;
  let entityService: jest.Mocked<EntityGenericService>;

  const entitySpec = {
    columns: ['title'],
    fields: { title: { type: 'string', column: 'Title' } },
    sort: 'title',
  };
  const qbSpec = {
    fields: { title: { type: 'string' } },
    columns: ['title'],
  };

  beforeEach(async () => {
    const entityServiceMock = {
      getQueryBuilderSpec: jest.fn().mockReturnValue(of(qbSpec)),
      getEntitySpec: jest.fn().mockReturnValue(of(entitySpec)),
      query: jest.fn().mockReturnValue(of(new HttpResponse({ body: { hits: [], totalHits: 0, searchAfter: [], pitId: null } }))),
      searchWithBql: jest.fn().mockReturnValue(of(new HttpResponse({ body: { hits: [], totalHits: 0, searchAfter: [], pitId: null } }))),
    } as unknown as jest.Mocked<EntityGenericService>;

    await TestBed.configureTestingModule({
      imports: [GenericListComponent],
      providers: [
        GenericListActionResolver,
        { provide: EntityGenericService, useValue: entityServiceMock },
        { provide: ViewService, useValue: { queryByEntity: () => of(new HttpResponse({ body: [] })) } },
        { provide: MessageService, useValue: { add: jest.fn(), clear: jest.fn() } },
        { provide: ConfirmationService, useValue: { confirm: jest.fn() } },
        {
          provide: ActivatedRoute,
          useValue: {
            paramMap: of(convertToParamMap({ entity: 'movie' })),
            snapshot: { data: {}, paramMap: convertToParamMap({ entity: 'movie' }) },
          },
        },
        { provide: Router, useValue: { navigate: jest.fn() } },
      ],
    })
      .overrideTemplate(GenericListComponent, '')
      .compileComponents();

    fixture = TestBed.createComponent(GenericListComponent);
    component = fixture.componentInstance;
    entityService = TestBed.inject(EntityGenericService) as jest.Mocked<EntityGenericService>;
  });

  it('initializes columns and loads data for the routed entity', fakeAsync(() => {
    fixture.detectChanges();
    tick();

    expect(component.columns.find(c => c.field === 'title')).toBeTruthy();
    expect(entityService.query).toHaveBeenCalledWith('movie', expect.objectContaining({ pageSize: component.itemsPerPage }));
  }));

  it('uses BQL search when a query is present', () => {
    component.entity = 'movie';
    component.currentQuery = 'title:star';

    component.loadPage();

    expect(entityService.searchWithBql).toHaveBeenCalledWith(
      'movie',
      'title:star',
      expect.objectContaining({ pageSize: component.itemsPerPage }),
    );
  });

  it('builds columns from spec with default helpers', () => {
    const cols = (component as any).buildColumnsFromSpec(entitySpec);

    expect(cols[0].field).toBe('lineNumber');
    expect(cols.some((c: any) => c.field === 'title' && c.header === 'Title')).toBe(true);
  });
});
