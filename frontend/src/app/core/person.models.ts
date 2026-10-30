export interface CreatePersonRequest {
  firstName: string;
  lastName: string;
  email: string;
  phone: string;
  profileBase64: string;
  profileFileName: string;
  profileContentType: string;
  birthDate: string;
  occupation: string;
  sex: string;
}

export interface PersonResponse extends Omit<CreatePersonRequest, 'profileBase64'> {
  id: number;
  createdAt: string;
}
