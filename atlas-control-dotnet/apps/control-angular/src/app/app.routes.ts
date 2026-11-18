import { Routes } from '@angular/router';
import { authGuard } from './auth/auth.guard';
import { LoginComponent } from './pages/login.component';
import { ApprovalsComponent } from './pages/approvals.component';
import { ApprovalDetailComponent } from './pages/approval-detail.component';
import { RequestComponent } from './pages/request.component';
import { PublicationsComponent } from './pages/publications.component';
import { PublicationDetailComponent } from './pages/publication-detail.component';
import { PublicationRequestComponent } from './pages/publication-request.component';
import { AuditComponent } from './pages/audit.component';
import { GrantsComponent } from './pages/grants.component';
import { RefundsComponent } from './pages/refunds.component';
import { OpsComponent } from './pages/ops.component';

export const routes: Routes = [
  { path: 'login', component: LoginComponent },
  { path: 'approvals', component: ApprovalsComponent, canActivate: [authGuard] },
  { path: 'approvals/:id', component: ApprovalDetailComponent, canActivate: [authGuard] },
  { path: 'approvals-new', component: RequestComponent, canActivate: [authGuard], data: { roles: ['inventory_operator', 'approver', 'iam_admin'] } },
  { path: 'publications', component: PublicationsComponent, canActivate: [authGuard] },
  { path: 'publications/:id', component: PublicationDetailComponent, canActivate: [authGuard] },
  { path: 'publications-new', component: PublicationRequestComponent, canActivate: [authGuard], data: { roles: ['catalog_editor', 'approver', 'iam_admin'] } },
  { path: 'audit', component: AuditComponent, canActivate: [authGuard], data: { roles: ['auditor', 'approver', 'iam_admin'] } },
  { path: 'grants', component: GrantsComponent, canActivate: [authGuard], data: { roles: ['iam_admin'] } },
  { path: 'refunds', component: RefundsComponent, canActivate: [authGuard], data: { roles: ['approver'] } },
  { path: 'ops', component: OpsComponent, canActivate: [authGuard] },
  { path: '', redirectTo: 'approvals', pathMatch: 'full' },
  { path: '**', redirectTo: 'approvals' },
];
