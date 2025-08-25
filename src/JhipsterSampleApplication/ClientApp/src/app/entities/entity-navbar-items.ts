import NavbarItem from 'app/layouts/navbar/navbar-item.model';

export const ENTITY_NAV_ITEMS: NavbarItem[] = [
  {
    name: 'Movie',
    route: '/movie',
    translationKey: 'global.menu.entities.movie',
  },
  {
    name: 'Birthday',
    route: '/birthday',
    translationKey: 'global.menu.entities.birthday',
  },
  {
    name: 'Supreme',
    route: '/supreme',
    translationKey: 'global.menu.entities.supreme',
  },
  {
    name: 'NamedQuery',
    route: '/named-query',
    translationKey: 'global.menu.entities.namedQuery',
  },
  {
    name: 'Selector',
    route: '/selector',
    translationKey: 'global.menu.entities.selector',
  },
  {
    name: 'View',
    route: '/view',
    translationKey: 'global.menu.entities.view',
  },
];
