import {UserClaimsModel} from 'src/app/common/models';
import {createReducer, on} from '@ngrx/store';
import {
  authenticate,
  connectionStringExistCount,
  loadAuthFailure, loadAuthState,
  loadAuthSuccess, logout,
  refreshToken,
  updateCurrentUserLocaleAndDarkTheme, updateDarkTheme, updateSideMenuOpened,
  updateUserInfo, updateUserLocale
} from './';
import {StoreStatusEnum} from 'src/app/common/const';

export const AUTH_REDUCER_NODE: string = 'auth';

export interface AuthCurrentUser {
  firstName: string;
  lastName: string;
  id: number;
  userName: string;
  locale?: string;
  languageId?: number;
  darkTheme?: boolean;
  loginRedirectUrl?: string;
  claims?: UserClaimsModel;
  isFirstUser: boolean
}

export interface AuthToken {
  accessToken: string;
  expiresIn: Date;
  tokenType: string;
  role: string;
}

export interface AuthState {
  token: AuthToken;
  currentUser: AuthCurrentUser;
  connectionString: {
    isConnectionStringExist: boolean;
    count: number;
  };
  sideMenuOpened: boolean;
  error: string;
  status: StoreStatusEnum;
}

export const authInitialState: AuthState = {
  token: {
    accessToken: '',
    expiresIn: null,
    tokenType: '',
    role: '',
  },
  currentUser: {
    firstName: '',
    lastName: '',
    id: 0,
    userName: '',
    locale: 'da', // TODO add env for test run
    languageId: 0,
    darkTheme: false,
    loginRedirectUrl: '',
    isFirstUser: false,
    claims: {
      unitsRead: false,
      unitsUpdate: false,
      workersCreate: false,
      workersRead: false,
      workersUpdate: false,
      workersDelete: false,
      sitesCreate: false,
      sitesRead: false,
      sitesUpdate: false,
      sitesDelete: false,
      entitySearchCreate: false,
      entitySearchRead: false,
      entitySearchUpdate: false,
      entitySearchDelete: false,
      entitySelectCreate: false,
      entitySelectRead: false,
      entitySelectUpdate: false,
      entitySelectDelete: false,
      deviceUsersCreate: false,
      deviceUsersRead: false,
      deviceUsersUpdate: false,
      deviceUsersDelete: false,
      usersCreate: false,
      usersRead: false,
      usersUpdate: false,
      usersDelete: false,
      eformsCreate: false,
      eformsDelete: false,
      eformsRead: false,
      eformsUpdateColumns: false,
      eformsDownloadXml: false,
      eformsUploadZip: false,
      casesRead: false,
      caseRead: false,
      caseUpdate: false,
      caseDelete: false,
      caseGetPdf: false,
      caseGetDocx: false,
      caseGetPptx: false,
      eformsPairingUpdate: false,
      eformsUpdateTags: false,
      eformsPairingRead: false,
      eformsReadTags: false,
      eformsGetCsv: false,
      eformsReadJasperReport: false,
      eformsUpdateJasperReport: false,
      eformAllowManagingEformTags: false,
    },
  },
  connectionString: {
    isConnectionStringExist: false,
    count: 0,
  },
  sideMenuOpened: true,
  error: null,
  status: StoreStatusEnum.Pending,
};

const _authReducer = createReducer(
  authInitialState,
  on(authenticate, (state) => ({
      ...state,
      status: StoreStatusEnum.Loading,
    }),
  ),
  on(connectionStringExistCount, (state, {payload}) => ({
    ...state,
    status: StoreStatusEnum.Success,
    connectionString: {
      isConnectionStringExist: payload.isConnectionStringExist,
      count: payload.count,
    },
  })),
  on(loadAuthSuccess, (state, {payload}) => ({
    ...state,
    status: StoreStatusEnum.Success,
    error: null,
    token: payload.token,
    currentUser: {
      ...state.currentUser,
      ...payload.currentUser
    },
    connectionString: {
      isConnectionStringExist: true,
      count: payload.count,
    },
  })),
  on(updateUserInfo, (state, {payload}) => ({
    ...state,
    status: StoreStatusEnum.Success,
    currentUser: {
      ...state.currentUser,
      darkTheme: payload.userSettings.model.darkTheme,
      locale: payload.userSettings.model.locale,
      loginRedirectUrl: payload.userSettings.model.loginRedirectUrl || '',
      claims: payload.userClaims,
      languageId: payload.userSettings.model.languageId,
    },
  })),
  on(updateCurrentUserLocaleAndDarkTheme, (state, {payload}) => ({
    ...state,
    status: StoreStatusEnum.Success,
    currentUser: {
      ...state.currentUser,
      darkTheme: payload.darkTheme,
      locale: payload.locale,
      languageId: payload.languageId,
    },
  })),
  on(loadAuthFailure, (state, {payload}) => ({
    ...state,
    error: payload,
    status: StoreStatusEnum.Error,
  })),
  on(loadAuthState, (_, {payload}) => ({
    ...payload.state,
  })),
  on(logout, () => ({
    ...authInitialState,
    status: StoreStatusEnum.Success,
  })),
  on(refreshToken, (state) => ({
    ...state,
    status: StoreStatusEnum.Loading,
  })),
  on(updateSideMenuOpened, (state, {payload}) => ({
    ...state,
    sideMenuOpened: payload.sideMenuIsOpened,
  })),
  on(updateUserLocale, (state, {payload}) => ({
    ...state,
    currentUser: {
      ...state.currentUser,
      locale: payload.locale,
      languageId: payload.languageId,
    }
  })),
  on(updateDarkTheme, (state, {payload}) => ({
    ...state,
    currentUser: {
      ...state.currentUser,
      darkTheme: payload
    }
  })),
);

export function authReducer(state: AuthState | undefined, action: any) {
  return _authReducer(state, action);
}
