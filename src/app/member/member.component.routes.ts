import { Routes }          from '@angular/router';
import { MemberComponent }   from './member.component';
import { MemberDetailsComponent } from '../member-details/member-details.component'

export const memberRoutes: Routes = [
  { path: 'members/:organisationId', component: MemberComponent },
  { path: 'members/:organisationId/details/:id', component: MemberDetailsComponent },
  { path: 'members/:organisationId/page/:page', component: MemberComponent }
];